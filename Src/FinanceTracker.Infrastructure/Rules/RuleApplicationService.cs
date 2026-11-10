using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Domain.Rules;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.OpenBanking.Transfers;
using FinanceTracker.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Rules;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IRuleApplicationService>]
public class RuleApplicationService : ServiceBase<RuleApplicationService>, IRuleApplicationService
{
    private readonly ITransferMatchingService _transferMatchingService;
    private readonly TimeProvider _timeProvider;

    public RuleApplicationService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<RuleApplicationService> logger,
        ITransferMatchingService transferMatchingService, TimeProvider timeProvider)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _transferMatchingService = transferMatchingService;
        _timeProvider = timeProvider;
    }

    public async Task<int> ApplyChangesAsync(DateTime firstRunFrom, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        DateTime startedAt = _timeProvider.UtcNow;

        RuleCheckpoint? checkpoint = await context.RuleCheckpoints.OwnedBy(UserId)
            .SingleOrDefaultAsync(cancellationToken);
        DateTime since = checkpoint?.AppliedFrom ?? firstRunFrom;

        List<Rule> rules = await LoadEnabledRulesAsync(context, cancellationToken);
        RuleRun run = RuleRun.Nothing;

        // Without rules there's nothing to apply, but the checkpoint still moves on so enabling one later doesn't
        // reach back over the transactions synced in the meantime
        if (rules.Count > 0)
        {
            List<BankTransaction> transactions = await context.BankTransactions.OwnedBy(UserId)
                .Where(x => x.Created > since || x.Updated > since)
                .ToListAsync(cancellationToken);

            run = Apply(rules, transactions);
        }

        if (checkpoint is null)
        {
            context.RuleCheckpoints.Add(new RuleCheckpoint(UserId, startedAt));
        }
        else
        {
            checkpoint.Record(startedAt);
        }

        await context.SaveChangesAsync(cancellationToken);
        Logger.LogAppliedRules(UserId, rules.Count, run.Considered, run.Changed);
        Report(run);

        return run.Changed;
    }

    public async Task<int> ReapplyAsync(Guid ruleId, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        List<BankTransaction> transactions = await context.BankTransactions.OwnedBy(UserId)
            .Where(x => x.RuleTags.Any(t => t.RuleId == ruleId) ||
                        (x.Transfer != null && x.Transfer.RuleId == ruleId) ||
                        (x.BillPayment != null && x.BillPayment.RuleId == ruleId) ||
                        (x.DebtPayment != null && x.DebtPayment.RuleId == ruleId))
            .ToListAsync(cancellationToken);

        if (transactions.Count == 0)
        {
            return 0;
        }

        // Taken before applying, the rule's transfer answers may be about to go
        List<Guid> decidedByTheRule = transactions
            .Where(x => x.Transfer?.RuleId == ruleId)
            .Select(static x => x.Id)
            .ToList();

        List<Rule> rules = await LoadEnabledRulesAsync(context, cancellationToken);
        RuleRun run = Apply(rules, transactions);

        await context.SaveChangesAsync(cancellationToken);
        Logger.LogReappliedRules(UserId, ruleId, run.Considered, run.Changed);
        Report(run);

        if (decidedByTheRule.Count > 0)
        {
            await _transferMatchingService.MatchAroundAsync(decidedByTheRule, cancellationToken);
        }

        return run.Changed;
    }

    public async Task<int> ApplyToExistingAsync(Guid ruleId, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        Rule rule = await context.Rules.OwnedBy(UserId).AsNoTracking()
            .SingleAsync(x => x.Id == ruleId, cancellationToken);
        if (!rule.IsEnabled)
        {
            return 0;
        }

        // Conditions test encrypted fields, so every transaction is loaded and checked once decrypted
        List<BankTransaction> transactions = await context.BankTransactions.OwnedBy(UserId)
            .ToListAsync(cancellationToken);

        List<BankTransaction> reached = transactions.Where(rule.Reaches).ToList();

        Dictionary<Guid, TransferInfo?> transfersBefore =
            reached.ToDictionary(static x => x.Id, static x => x.Transfer);

        List<Rule> rules = await LoadEnabledRulesAsync(context, cancellationToken);
        RuleRun run = Apply(rules, reached);

        await context.SaveChangesAsync(cancellationToken);
        Logger.LogAppliedRuleToExisting(UserId, ruleId, run.Considered, run.Changed);
        Report(run);

        List<Guid> transferChanged = reached
            .Where(x => TransferChanged(transfersBefore[x.Id], x.Transfer))
            .Select(static x => x.Id)
            .ToList();

        if (transferChanged.Count > 0)
        {
            await _transferMatchingService.MatchAroundAsync(transferChanged, cancellationToken);
        }

        return run.Changed;
    }

    private static bool TransferChanged(TransferInfo? before, TransferInfo? after)
    {
        if (before is null)
        {
            return after is not null;
        }

        return !before.IsSameAs(after);
    }

    private async Task<List<Rule>> LoadEnabledRulesAsync(FinanceTrackerContext context,
        CancellationToken cancellationToken) =>
        await context.Rules.OwnedBy(UserId)
            .Where(x => x.IsEnabled)
            .ToListAsync(cancellationToken);

    private RuleRun Apply(List<Rule> rules, List<BankTransaction> transactions)
    {
        DateTime decidedAt = _timeProvider.UtcNow;
        int changed = 0;
        Dictionary<Guid, int> matches = [];

        foreach (BankTransaction transaction in transactions)
        {
            RuleOutcome outcome = RuleEvaluator.Evaluate(rules, transaction);
            foreach (Guid ruleId in outcome.MatchedRuleIds)
            {
                matches[ruleId] = matches.GetValueOrDefault(ruleId) + 1;
            }

            if (transaction.ApplyRules(outcome, decidedAt))
            {
                changed++;
            }
        }

        return new RuleRun(rules.Count, transactions.Count, changed, matches);
    }

    /// <summary>
    /// Tags the span the rules ran in with what they did, and logs how many transactions each rule matched at
    /// Debug, for working out why a rule does or doesn't fire.
    /// </summary>
    private void Report(RuleRun run)
    {
        InfrastructureTelemetry.TagCurrent(
            ("fintrack.rules", run.Rules),
            ("fintrack.rules.matched", run.Matches.Count),
            ("fintrack.transactions.considered", run.Considered),
            ("fintrack.transactions.changed", run.Changed));

        foreach ((Guid ruleId, int matched) in run.Matches)
        {
            Logger.LogRuleMatched(UserId, ruleId, matched);
        }
    }
}
