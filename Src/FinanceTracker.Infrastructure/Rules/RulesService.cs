using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.Rules;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
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
    private readonly TimeProvider _timeProvider;

    public RulesService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<RulesService> logger,
        IRuleApplicationService ruleApplicationService, TimeProvider timeProvider)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _ruleApplicationService = ruleApplicationService;
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
    }

    public async Task<Rule> GetRuleAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainRule rule = await context.Rules.OwnedBy(UserId)
            .AsNoTracking()
            .SingleAsync(x => x.Id == id, cancellationToken);

        return RuleMapper.ToContract(rule);
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

        return RuleMapper.ToContract(newRule);
    }

    public async Task<Rule> UpdateRuleAsync(Guid id, SaveRule rule, CancellationToken cancellationToken)
    {
        DomainRule updated;
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        updated = await context.Rules.OwnedBy(UserId).SingleAsync(x => x.Id == id, cancellationToken);

        await EnsureTheUsersOwnAsync(context, rule, cancellationToken);

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

        await _ruleApplicationService.ReapplyAsync(id, cancellationToken);

        return RuleMapper.ToContract(updated);
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
    }

    public async Task<RulePreview> PreviewRuleAsync(Guid? id, SaveRule rule, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureTheUsersOwnAsync(context, rule, cancellationToken);

        // Untracked, the draft and the transactions are changed in memory and never saved
        List<DomainRule> saved = await context.Rules.OwnedBy(UserId).AsNoTracking().ToListAsync(cancellationToken);
        DomainRule draft = DraftOf(id, rule, saved);

        List<DomainRule> running = saved
            .Where(x => x.IsEnabled && x.Id != draft.Id)
            .Append(draft)
            .ToList();

        Dictionary<Guid, string> tagNames = await context.CustomClassifications.OwnedBy(UserId)
            .ToDictionaryAsync(static x => x.Id, static x => x.Tag, cancellationToken);

        // Conditions test encrypted fields, so every transaction is loaded and checked once decrypted
        List<BankTransaction> transactions = await context.BankTransactions.OwnedBy(UserId).AsNoTracking()
            .ToListAsync(cancellationToken);

        return RulePreviewer.Preview(running, draft, transactions, tagNames, _timeProvider.GetUtcNow().UtcDateTime);
    }

    public Task<int> ApplyRuleToExistingAsync(Guid id, CancellationToken cancellationToken) =>
        _ruleApplicationService.ApplyToExistingAsync(id, cancellationToken);

    public async Task<SaveRule> GetDraftFromTransactionAsync(Guid transactionId,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        BankTransaction transaction = await context.BankTransactions.OwnedBy(UserId).AsNoTracking()
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
        }

        // With the rule gone, what it gave is taken back and any later rule that also matches takes over
        await _ruleApplicationService.ReapplyAsync(id, cancellationToken);
    }

    /// <summary>
    /// The page only offers the user's own tags and accounts, this covers calls that don't come from it.
    /// </summary>
    /// <exception cref="InvalidOperationException">An action adds a tag, or a condition names an account, that
    /// doesn't exist or isn't the user's.</exception>
    private async Task EnsureTheUsersOwnAsync(FinanceTrackerContext context, SaveRule rule,
        CancellationToken cancellationToken)
    {
        await EnsureTagsAreTheUsersAsync(context, rule, cancellationToken);
        await EnsureAccountsAreTheUsersAsync(context, rule, cancellationToken);
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

        int owned = await context.BankAccounts.OwnedBy(UserId)
            .CountAsync(x => accountIds.Contains(x.Id), cancellationToken);

        if (owned != accountIds.Count)
        {
            throw new InvalidOperationException("A rule can only look at the user's own accounts.");
        }
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
