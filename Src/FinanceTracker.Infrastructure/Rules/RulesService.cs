using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.Rules;
using FinanceTracker.Contracts.Services;
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

    public RulesService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<RulesService> logger,
        IRuleApplicationService ruleApplicationService) : base(currentUser, financeTrackerContextFactory, logger)
    {
        _ruleApplicationService = ruleApplicationService;
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

        updated.Rename(rule.Name);
        updated.ChangeMatchMode(RuleMapper.ToDomain(rule.MatchMode));
        updated.StopProcessingAfterMatch(rule.StopProcessing);
        updated.ReplaceConditions(ToDomainConditions(rule));
        updated.ReplaceActions(ToDomainActions(rule));

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

    private static List<DomainRuleCondition> ToDomainConditions(SaveRule rule) =>
        rule.Conditions.Select(RuleMapper.ToDomain).ToList();

    private static List<DomainRuleAction> ToDomainActions(SaveRule rule) =>
        rule.Actions.Select(RuleMapper.ToDomain).ToList();
}
