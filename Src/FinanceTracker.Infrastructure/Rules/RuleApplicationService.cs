using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Domain.Rules;
using FinanceTracker.Domain.Sharing;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.OpenBanking.Transfers;
using FinanceTracker.Infrastructure.Sharing;
using FinanceTracker.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Rules;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IRuleApplicationService>]
public class RuleApplicationService : ServiceBase<RuleApplicationService>, IRuleApplicationService
{
    private readonly ITransferMatchingService _transferMatchingService;
    private readonly IAccountScope _accountScope;
    private readonly TimeProvider _timeProvider;
    private readonly FinTrackMetrics _metrics;

    public RuleApplicationService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<RuleApplicationService> logger,
        ITransferMatchingService transferMatchingService, IAccountScope accountScope, TimeProvider timeProvider,
        FinTrackMetrics metrics)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _metrics = metrics;
        _transferMatchingService = transferMatchingService;
        _accountScope = accountScope;
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

        // Someone else's rules may reach their transactions, so they're run even when the user has none
        List<BankTransaction> transactions = await (await ChangeableAsync(context, cancellationToken))
            .Where(x => x.Created > since || x.Updated > since)
            .ToListAsync(cancellationToken);
        RuleReach reach = await RuleReach.ForAsync(context, transactions, cancellationToken);
        RuleRun run = await ApplyAsync(context, reach, transactions, cancellationToken);

        // The checkpoint moves on even when nothing ran, so enabling a rule later doesn't reach back over the
        // transactions synced in the meantime
        if (checkpoint is null)
        {
            context.RuleCheckpoints.Add(new RuleCheckpoint(UserId, startedAt));
        }
        else
        {
            checkpoint.Record(startedAt);
        }

        await context.SaveChangesAsync(cancellationToken);
        Logger.LogAppliedRules(UserId, run.Rules, run.Considered, run.Changed);
        Report(run);

        return run.Changed;
    }

    public async Task<int> ReapplyAsync(Guid ruleId, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Its owner's reach, the user may be someone they let edit it
        Guid ownerId = await context.Rules.Where(x => x.Id == ruleId).Select(static x => (Guid?)x.UserId)
            .SingleOrDefaultAsync(cancellationToken) ?? UserId;
        AccountsInView ownersReach = await AccountScope.ResolveChangeableForAsync(context, ownerId, cancellationToken);
        List<BankTransaction> transactions = await context.BankTransactions.ChangeableIn(ownersReach)
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

        RuleReach reach = await RuleReach.ForAsync(context, transactions, cancellationToken);
        RuleRun run = await ApplyAsync(context, reach, transactions, cancellationToken);

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
        List<BankTransaction> transactions = await (await ChangeableAsync(context, cancellationToken))
            .ToListAsync(cancellationToken);
        List<BankTransaction> reached = transactions.Where(rule.Reaches).ToList();

        int changed = await ApplyAndRematchAsync(context, reached, cancellationToken);
        Logger.LogAppliedRuleToExisting(UserId, ruleId, reached.Count, changed);
        return changed;
    }

    public async Task<int> ReapplyToAccountsAsync(IReadOnlyCollection<Guid> accountIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountIds);
        if (accountIds.Count == 0)
        {
            return 0;
        }

        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        List<BankTransaction> transactions = await (await ChangeableAsync(context, cancellationToken))
            .Where(x => accountIds.Contains(x.AccountId))
            .ToListAsync(cancellationToken);

        int changed = await ApplyAndRematchAsync(context, transactions, cancellationToken);
        Logger.LogChangedTheir(UserId, "re-applied rules to", "shared accounts");
        return changed;
    }

    /// <summary>
    /// The transactions the user can change: their own, and those on accounts shared with them with changes allowed.
    /// </summary>
    private async Task<IQueryable<BankTransaction>> ChangeableAsync(FinanceTrackerContext context,
        CancellationToken cancellationToken)
    {
        AccountsInView changeable = await _accountScope.ResolveChangeableAsync(context, cancellationToken);
        return context.BankTransactions.ChangeableIn(changeable);
    }

    /// <summary>
    /// Applies the rules that reach <paramref name="transactions"/>, saves, then re-matches transfers around those
    /// whose transfer answer changed.
    /// </summary>
    /// <returns>How many changed.</returns>
    private async Task<int> ApplyAndRematchAsync(FinanceTrackerContext context, List<BankTransaction> transactions,
        CancellationToken cancellationToken)
    {
        Dictionary<Guid, TransferInfo?> transfersBefore =
            transactions.ToDictionary(static x => x.Id, static x => x.Transfer);

        RuleReach reach = await RuleReach.ForAsync(context, transactions, cancellationToken);
        RuleRun run = await ApplyAsync(context, reach, transactions, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        Report(run);

        List<Guid> transferChanged = transactions
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

    /// <summary>
    /// Runs the rules that reach each of <paramref name="transactions"/> over it. Another person's tag, bill or debt
    /// their rule puts on a transaction is shared with its owner, so they see its name. Tracked changes, saved by the
    /// caller.
    /// </summary>
    private async Task<RuleRun> ApplyAsync(FinanceTrackerContext context, RuleReach reach,
        List<BankTransaction> transactions, CancellationToken cancellationToken)
    {
        // Even with no rules left, so what a disabled or deleted one gave is taken back
        DateTime decidedAt = _timeProvider.UtcNow;
        int changed = 0;
        Dictionary<Guid, int> matches = [];

        foreach (BankTransaction transaction in transactions)
        {
            RuleOutcome outcome = RuleEvaluator.Evaluate(reach.RulesFor(transaction), transaction);
            foreach (Guid ruleId in outcome.MatchedRuleIds)
            {
                matches[ruleId] = matches.GetValueOrDefault(ruleId) + 1;
            }

            if (transaction.ApplyRules(outcome, decidedAt))
            {
                changed++;
                await ShareWhatOthersRulesPutOnAsync(context, reach, transaction, decidedAt, cancellationToken);
            }
        }

        return new RuleRun(reach.Rules.Count, transactions.Count, changed, matches);
    }

    /// <summary>
    /// Shares the tags, bill and debt another person's rules put on <paramref name="transaction"/> with its owner, as
    /// when that person puts them on by hand.
    /// </summary>
    private static async Task ShareWhatOthersRulesPutOnAsync(FinanceTrackerContext context, RuleReach reach,
        BankTransaction transaction, DateTime now, CancellationToken cancellationToken)
    {
        List<(Guid RuleId, ShareItemType ItemType, Guid ItemId)> fromRules =
        [
            .. transaction.RuleTags.Select(static x => (x.RuleId, ShareItemType.Classification, x.ClassificationId))
        ];

        if (transaction.BillPayment is { RuleId: { } billRuleId, BillId: { } billId })
        {
            fromRules.Add((billRuleId, ShareItemType.Bill, billId));
        }

        if (transaction.DebtPayment is { RuleId: { } debtRuleId, DebtId: { } debtId })
        {
            fromRules.Add((debtRuleId, ShareItemType.Debt, debtId));
        }

        foreach ((Guid ruleId, ShareItemType itemType, Guid itemId) in fromRules)
        {
            if (reach.OwnerOf(ruleId) is { } ruleOwner && ruleOwner != transaction.UserId)
            {
                await AnnotationShares.ShareWithOwnerAsync(context, ruleOwner, transaction, itemType, itemId, now,
                    cancellationToken);
            }
        }
    }

    /// <summary>
    /// Tags the span the rules ran in with what they did, and logs how many transactions each rule matched at
    /// Debug, for working out why a rule does or doesn't fire.
    /// </summary>
    private void Report(RuleRun run)
    {
        CurrentActivity.Tag(
            (TelemetryTags.Rules, run.Rules),
            (TelemetryTags.RulesMatched, run.Matches.Count),
            (TelemetryTags.TransactionsConsidered, run.Considered),
            (TelemetryTags.TransactionsChanged, run.Changed));
        _metrics.RulesApplied(run.Changed);

        foreach ((Guid ruleId, int matched) in run.Matches)
        {
            Logger.LogRuleMatched(UserId, ruleId, matched);
        }
    }
}
