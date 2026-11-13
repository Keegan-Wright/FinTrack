using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.Rules;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Sharing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DomainRule = FinanceTracker.Domain.Rules.Rule;
using DomainRuleAction = FinanceTracker.Domain.Rules.RuleAction;
using DomainRuleCondition = FinanceTracker.Domain.Rules.RuleCondition;

namespace FinanceTracker.Infrastructure.Rules;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IRulesService>]
public class RulesService : ServiceBase<RulesService>, IRulesService
{
    private readonly IRuleApplicationService _ruleApplicationService;
    private readonly IAccountScope _accountScope;
    private readonly TimeProvider _timeProvider;

    public RulesService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<RulesService> logger,
        IRuleApplicationService ruleApplicationService, IAccountScope accountScope, TimeProvider timeProvider)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _ruleApplicationService = ruleApplicationService;
        _accountScope = accountScope;
        _timeProvider = timeProvider;
    }

    public async IAsyncEnumerable<Rule> GetRulesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Ordered as the evaluator runs them, by order then id
        IAsyncEnumerable<DomainRule> rules = context.Rules.OwnedBy(UserId)
            .AsNoTracking()
            .OrderBy(static x => x.Order)
            .ThenBy(static x => x.Id)
            .AsAsyncEnumerable();

        await foreach (DomainRule rule in rules.WithCancellation(cancellationToken))
        {
            yield return RuleMapper.ToContract(rule);
        }

        // Then those other people share with them, which run where their owner's rules run
        List<VisibleItem<DomainRule>> shared = await SharedRules.WithAsync(context, UserId, cancellationToken);
        if (shared.Count == 0)
        {
            yield break;
        }

        AccountsInView seen = await _accountScope.ResolveAllAsync(context, cancellationToken);
        foreach (VisibleItem<DomainRule> rule in shared)
        {
            yield return await SharedRules.ToContractAsync(context, UserId, seen, rule, cancellationToken);
        }
    }

    public async Task<Rule> GetRuleAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainRule? rule = await context.Rules.OwnedBy(UserId)
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (rule is not null)
        {
            return RuleMapper.ToContract(rule);
        }

        VisibleItem<DomainRule> shared = await SharedRuleAsync(context, id, mustChange: false, cancellationToken);
        AccountsInView seen = await _accountScope.ResolveAllAsync(context, cancellationToken);
        return await SharedRules.ToContractAsync(context, UserId, seen, shared, cancellationToken);
    }

    public async Task<Rule> AddRuleAsync(SaveRule rule, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        await EnsureTheUsersOwnAsync(context, rule, cancellationToken);

        int? lastOrder = await context.Rules.OwnedBy(UserId)
            .MaxAsync(static x => (int?)x.Order, cancellationToken);

        DomainRule newRule = new(UserId, rule.Name, (lastOrder ?? -1) + 1, RuleMapper.ToDomain(rule.MatchMode),
            ToDomainConditions(rule), ToDomainActions(rule));
        newRule.StopProcessingAfterMatch(rule.StopProcessing);

        if (!rule.IsEnabled)
        {
            newRule.Disable();
        }

        context.Rules.Add(newRule);
        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChanged(UserId, "created", "rule", newRule.Id);

        return RuleMapper.ToContract(newRule);
    }

    public async Task<Rule> UpdateRuleAsync(Guid id, SaveRule rule, CancellationToken cancellationToken)
    {
        DomainRule updated;
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Their own, or one shared with them with changes allowed, using only what they may
        updated = await context.Rules.SingleAsync(x => x.Id == id, cancellationToken);
        if (updated.UserId == UserId)
        {
            await EnsureTheUsersOwnAsync(context, rule, cancellationToken);
        }
        else
        {
            VisibleItem<DomainRule> shared = await SharedRuleAsync(context, id, mustChange: true, cancellationToken);
            AccountsInView seen = await _accountScope.ResolveAllAsync(context, cancellationToken);
            await SharedRules.EnsureAllowedAsync(context, UserId, seen, shared.Item, rule, cancellationToken);
        }

        Overwrite(updated, rule);

        if (rule.IsEnabled)
        {
            updated.Enable();
        }
        else
        {
            updated.Disable();
        }

        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChanged(UserId, "updated", "rule", id);

        await _ruleApplicationService.ReapplyAsync(id, cancellationToken);

        if (updated.UserId == UserId)
        {
            return RuleMapper.ToContract(updated);
        }

        // Still shared as it was, as they'll see it
        VisibleItem<DomainRule> saved = await SharedRuleAsync(context, id, mustChange: true, cancellationToken);
        AccountsInView visible = await _accountScope.ResolveAllAsync(context, cancellationToken);
        return await SharedRules.ToContractAsync(context, UserId, visible, saved, cancellationToken);
    }

    public async Task ReorderRulesAsync(ReorderRules order, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        Dictionary<Guid, DomainRule> rules = await context.Rules.OwnedBy(UserId)
            .ToDictionaryAsync(static x => x.Id, cancellationToken);

        if (order.RuleIds.Count != rules.Count || !order.RuleIds.All(rules.ContainsKey))
        {
            throw new InvalidOperationException("The order has to list each of the user's rules once.");
        }

        for (int position = 0; position < order.RuleIds.Count; position++)
        {
            rules[order.RuleIds[position]].MoveTo(position);
        }

        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChangedTheir(UserId, "reordered", "rules");
    }

    public async Task<RulePreview> PreviewRuleAsync(Guid? id, SaveRule rule, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Untracked, the draft and the transactions are changed in memory and never saved
        DomainRule draft = await PreviewDraftAsync(context, id, rule, cancellationToken);

        // Conditions test encrypted fields, so every transaction they can change is loaded and checked once decrypted
        AccountsInView changeable = await _accountScope.ResolveChangeableAsync(context, cancellationToken);
        List<BankTransaction> loaded = await context.BankTransactions.ChangeableIn(changeable).AsNoTracking()
            .ToListAsync(cancellationToken);

        // Only those the draft's owner's rules reach. Each with every rule that reaches it, the draft standing in for
        // its saved self
        RuleReach reach = await RuleReach.ForAsync(context, loaded, cancellationToken);
        List<BankTransaction> transactions = loaded.Where(x => reach.Reaches(draft.UserId, x)).ToList();
        Func<BankTransaction, IReadOnlyList<DomainRule>> rulesFor = transaction => reach.RulesFor(transaction)
            .Where(x => x.Id != draft.Id)
            .Append(draft)
            .ToList();

        // Anyone's whose rules reach them may change them
        List<Guid> people = reach.Rules.Select(static x => x.UserId).Append(UserId).Append(draft.UserId)
            .Distinct()
            .ToList();
        Dictionary<Guid, string> tagNames = await context.CustomClassifications.OwnedByAny(people)
            .ToDictionaryAsync(static x => x.Id, static x => x.Tag, cancellationToken);
        Dictionary<Guid, string> billNames = await context.Bills.OwnedByAny(people)
            .ToDictionaryAsync(static x => x.Id, static x => x.Name, cancellationToken);
        Dictionary<Guid, string> debtNames = await context.Debts.OwnedByAny(people)
            .ToDictionaryAsync(static x => x.Id, static x => x.Name, cancellationToken);

        RulePreviewNames names = new(tagNames, billNames, debtNames);
        return RulePreviewer.Preview(rulesFor, draft, transactions, names, changeable.DescriptionOf,
            _timeProvider.UtcNow);
    }

    public Task<int> ApplyRuleToExistingAsync(Guid id, CancellationToken cancellationToken) =>
        _ruleApplicationService.ApplyToExistingAsync(id, cancellationToken);

    public async Task<SaveRule> GetDraftFromTransactionAsync(Guid transactionId,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Their own, or one they can change on an account shared with them, which their rules now reach too
        AccountsInView changeable = await _accountScope.ResolveChangeableAsync(context, cancellationToken);
        BankTransaction transaction = await context.BankTransactions.ChangeableIn(changeable).AsNoTracking()
            .SingleAsync(x => x.Id == transactionId, cancellationToken);

        List<Guid> tagIds = await context.CustomClassifications.OwnedBy(UserId)
            .Select(static x => x.Id)
            .ToListAsync(cancellationToken);

        return RuleMapper.DraftFrom(transaction, tagIds.ToHashSet());
    }

    public async Task DeleteRuleAsync(Guid id, CancellationToken cancellationToken)
    {
        await using (FinanceTrackerContext context =
                     await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken))
        {
            DomainRule rule = await context.Rules.OwnedBy(UserId).SingleAsync(x => x.Id == id, cancellationToken);
            context.Rules.Remove(rule);
            await context.SaveChangesAsync(cancellationToken);
            Logger.LogChanged(UserId, "deleted", "rule", id);
        }

        // With the rule gone, what it gave is taken back and any later rule that also matches takes over
        await _ruleApplicationService.ReapplyAsync(id, cancellationToken);
    }

    /// <summary>
    /// The page only offers the user's own tags, bills and debts and the accounts they see, this covers calls that
    /// don't come from it.
    /// </summary>
    /// <exception cref="InvalidOperationException">An action adds a tag or pays a bill or debt, or a condition names
    /// an account, that doesn't exist or isn't the user's.</exception>
    private async Task EnsureTheUsersOwnAsync(FinanceTrackerContext context, SaveRule rule,
        CancellationToken cancellationToken)
    {
        await EnsureTagsAreTheUsersAsync(context, rule, cancellationToken);
        await EnsureBillsAreTheUsersAsync(context, rule, cancellationToken);
        await EnsureDebtsAreTheUsersAsync(context, rule, cancellationToken);
        await EnsureAccountsAreTheUsersAsync(context, rule, cancellationToken);
    }

    private async Task EnsureBillsAreTheUsersAsync(FinanceTrackerContext context, SaveRule rule,
        CancellationToken cancellationToken)
    {
        List<Guid> billIds = rule.Actions
            .Select(static x => x.BillId)
            .OfType<Guid>()
            .Distinct()
            .ToList();

        if (billIds.Count == 0)
        {
            return;
        }

        int owned = await context.Bills.OwnedBy(UserId)
            .CountAsync(x => billIds.Contains(x.Id), cancellationToken);

        if (owned != billIds.Count)
        {
            throw new InvalidOperationException("A rule can only pay the user's own bills.");
        }
    }

    private async Task EnsureDebtsAreTheUsersAsync(FinanceTrackerContext context, SaveRule rule,
        CancellationToken cancellationToken)
    {
        List<Guid> debtIds = rule.Actions
            .Select(static x => x.DebtId)
            .OfType<Guid>()
            .Distinct()
            .ToList();

        if (debtIds.Count == 0)
        {
            return;
        }

        int owned = await context.Debts.OwnedBy(UserId)
            .CountAsync(x => debtIds.Contains(x.Id), cancellationToken);

        if (owned != debtIds.Count)
        {
            throw new InvalidOperationException("A rule can only pay off the user's own debts.");
        }
    }

    private async Task EnsureTagsAreTheUsersAsync(FinanceTrackerContext context, SaveRule rule,
        CancellationToken cancellationToken)
    {
        List<Guid> tagIds = rule.Actions
            .Select(static x => x.ClassificationId)
            .OfType<Guid>()
            .Distinct()
            .ToList();

        if (tagIds.Count == 0)
        {
            return;
        }

        int owned = await context.CustomClassifications.OwnedBy(UserId)
            .CountAsync(x => tagIds.Contains(x.Id), cancellationToken);

        if (owned != tagIds.Count)
        {
            throw new InvalidOperationException("A rule can only add the user's own tags.");
        }
    }

    private async Task EnsureAccountsAreTheUsersAsync(FinanceTrackerContext context, SaveRule rule,
        CancellationToken cancellationToken)
    {
        // Validated as ids before the request gets here
        List<Guid> accountIds = rule.Conditions
            .Where(static x => x.Field == RuleField.Account)
            .Select(static x => Guid.Parse(x.Value))
            .Distinct()
            .ToList();

        if (accountIds.Count == 0)
        {
            return;
        }

        // Any they can see, a rule only reaches the transactions they can change though
        AccountsInView all = await _accountScope.ResolveAllAsync(context, cancellationToken);
        int seen = await context.BankAccounts.InView(all)
            .CountAsync(x => accountIds.Contains(x.Id), cancellationToken);

        if (seen != accountIds.Count)
        {
            throw new InvalidOperationException("A rule can only look at the accounts the user can see.");
        }
    }

    /// <summary>
    /// The rule to preview. One of theirs or a new one, see <see cref="DraftOf"/>, or one shared with them with changes
    /// allowed, with the edit checked as for saving.
    /// </summary>
    private async Task<DomainRule> PreviewDraftAsync(FinanceTrackerContext context, Guid? id, SaveRule rule,
        CancellationToken cancellationToken)
    {
        List<DomainRule> saved = await context.Rules.OwnedBy(UserId).AsNoTracking().ToListAsync(cancellationToken);
        if (id is not { } ruleId || saved.Exists(x => x.Id == ruleId))
        {
            await EnsureTheUsersOwnAsync(context, rule, cancellationToken);
            return DraftOf(id, rule, saved);
        }

        VisibleItem<DomainRule> shared = await SharedRuleAsync(context, ruleId, mustChange: true, cancellationToken);
        AccountsInView seen = await _accountScope.ResolveAllAsync(context, cancellationToken);
        await SharedRules.EnsureAllowedAsync(context, UserId, seen, shared.Item, rule, cancellationToken);

        DomainRule draft = shared.Item;
        Overwrite(draft, rule);
        draft.Enable();
        return draft;
    }

    /// <summary>
    /// Rule <paramref name="id"/>, shared with the user.
    /// </summary>
    /// <param name="mustChange">Whether they must be allowed to change it.</param>
    /// <exception cref="InvalidOperationException">It isn't shared with them, or not so they can change it.</exception>
    private async Task<VisibleItem<DomainRule>> SharedRuleAsync(FinanceTrackerContext context, Guid id, bool mustChange,
        CancellationToken cancellationToken)
    {
        VisibleItem<DomainRule>? shared = (await SharedRules.WithAsync(context, UserId, cancellationToken))
            .SingleOrDefault(x => x.Item.Id == id);
        if (shared is null || (mustChange && !shared.CanChange))
        {
            throw new InvalidOperationException($"No rule {id} the user can change was found.");
        }

        return shared;
    }

    /// <summary>
    /// <paramref name="rule"/> as a domain rule to preview, switched on whatever it says. The saved one with the
    /// changes when editing so it keeps its id and place, or a new one running last.
    /// </summary>
    /// <param name="saved">The user's rules, untracked.</param>
    /// <exception cref="InvalidOperationException">None of <paramref name="saved"/> has <paramref name="id"/>.
    /// </exception>
    private DomainRule DraftOf(Guid? id, SaveRule rule, List<DomainRule> saved)
    {
        DomainRule draft;
        if (id is { } ruleId)
        {
            draft = saved.Single(x => x.Id == ruleId);
            Overwrite(draft, rule);
        }
        else
        {
            int order = saved.Count == 0 ? 0 : saved.Max(static x => x.Order) + 1;
            draft = new DomainRule(UserId, rule.Name, order, RuleMapper.ToDomain(rule.MatchMode),
                ToDomainConditions(rule), ToDomainActions(rule));
        }

        draft.Enable();
        return draft;
    }

    /// <summary>
    /// Everything about <paramref name="target"/> from <paramref name="rule"/> except its order and whether it's on.
    /// </summary>
    private static void Overwrite(DomainRule target, SaveRule rule)
    {
        target.Rename(rule.Name);
        target.ChangeMatchMode(RuleMapper.ToDomain(rule.MatchMode));
        target.StopProcessingAfterMatch(rule.StopProcessing);
        target.ReplaceConditions(ToDomainConditions(rule));
        target.ReplaceActions(ToDomainActions(rule));
    }

    private static List<DomainRuleCondition> ToDomainConditions(SaveRule rule) =>
        rule.Conditions.Select(RuleMapper.ToDomain).ToList();

    private static List<DomainRuleAction> ToDomainActions(SaveRule rule) =>
        rule.Actions.Select(RuleMapper.ToDomain).ToList();
}
