using FinanceTracker.Domain.Household;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Persistence;
using FinanceTracker.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.OpenBanking.Transfers;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<ITransferMatchingService>]
public class TransferMatchingService : ServiceBase<TransferMatchingService>, ITransferMatchingService
{
    private readonly TimeProvider _timeProvider;
    private readonly FinTrackMetrics _metrics;

    public TransferMatchingService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<TransferMatchingService> logger,
        TimeProvider timeProvider, FinTrackMetrics metrics) : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
        _metrics = metrics;
    }

    public async Task<TransferMatchSummary> MatchAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        DateTime startedAt = _timeProvider.UtcNow;

        // Amounts and dates are encrypted, so matching happens in memory. Tracked, so saving writes only the
        // transactions the matcher changed
        TransferGroup group = await TransferGroup.ForAsync(context, UserId, cancellationToken);
        List<BankTransaction> transactions =
            await group.Transactions(context).ToListAsync(cancellationToken);

        return await MatchAndSaveAsync(context, group, transactions, startedAt, recordCheckpoint: true,
            cancellationToken);
    }

    public async Task<TransferMatchSummary?> MatchChangesAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        DateTime startedAt = _timeProvider.UtcNow;

        TransferMatchCheckpoint? checkpoint = await context.TransferMatchCheckpoints.OwnedBy(UserId)
            .SingleOrDefaultAsync(cancellationToken);
        TransferGroup group = await TransferGroup.ForAsync(context, UserId, cancellationToken);
        if (checkpoint is null)
        {
            List<BankTransaction> everything =
                await group.Transactions(context).ToListAsync(cancellationToken);
            return await MatchAndSaveAsync(context, group, everything, startedAt, recordCheckpoint: true,
                cancellationToken);
        }

        DateTime since = checkpoint.MatchedFrom;
        List<DateTime> changed = await context.BankTransactions.OwnedBy(UserId)
            .Where(x => x.Created > since || x.Updated > since)
            .Select(static x => x.OccurredAt)
            .ToListAsync(cancellationToken);

        if (changed.Count == 0)
        {
            Logger.LogNoTransferChanges(UserId);
            TagNothingMatched();
            return null;
        }

        return await MatchAroundAsync(context, group, changed, startedAt, recordCheckpoint: true, cancellationToken);
    }

    public async Task<TransferMatchSummary> MatchAroundAsync(IReadOnlyCollection<Guid> transactionIds,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DateTime startedAt = _timeProvider.UtcNow;

        // The other half of a pair may be someone else's in the group
        TransferGroup group = await TransferGroup.ForAsync(context, UserId, cancellationToken);
        List<DateTime> dates = await group.Transactions(context)
            .Where(x => transactionIds.Contains(x.Id))
            .Select(static x => x.OccurredAt)
            .ToListAsync(cancellationToken);

        if (dates.Count == 0)
        {
            TagNothingMatched();
            return new TransferMatchSummary(0, 0, 0);
        }

        // A sync saved since the last run hasn't been matched yet, so the checkpoint stays put for that run to find it
        return await MatchAroundAsync(context, group, dates, startedAt, recordCheckpoint: false, cancellationToken);
    }

    // The span still says what matching did when there was nothing to look at
    private static void TagNothingMatched() =>
        CurrentActivity.Tag(
            (TelemetryTags.TransactionsConsidered, 0),
            (TelemetryTags.Transfers, 0),
            (TelemetryTags.TransfersToReview, 0),
            (TelemetryTags.TransfersCleared, 0));

    /// <summary>
    /// Matches the transactions near <paramref name="dates"/>. Something on those dates can only affect transactions
    /// close enough to be its mirror, so the window runs that far either side, and up to now as late ones are the
    /// likeliest to change.
    /// </summary>
    private async Task<TransferMatchSummary> MatchAroundAsync(FinanceTrackerContext context, TransferGroup group,
        List<DateTime> dates, DateTime startedAt, bool recordCheckpoint, CancellationToken cancellationToken)
    {
        DateTime from = dates.Min().AddDays(-TransferMatcher.MaxDaysApart);
        DateTime to = (dates.Max() > startedAt ? dates.Max() : startedAt).AddDays(TransferMatcher.MaxDaysApart);

        List<BankTransaction> transactions = await group.Transactions(context)
            .WhereEncryptedDayBetween(x => x.OccurredAt, from, to, context)
            .ToListAsync(cancellationToken);

        transactions.AddRange(await LoadPartnersOutsideAsync(context, group, transactions, cancellationToken));

        return await MatchAndSaveAsync(context, group, transactions, startedAt, recordCheckpoint, cancellationToken);
    }

    /// <summary>
    /// The transactions paired with <paramref name="loaded"/> that it doesn't include, either way round. The matcher
    /// treats what it's given as the whole picture, without them it would clear a match whose other half is outside
    /// the window, and could pair a user's confirmed partner with something else.
    /// </summary>
    private static async Task<List<BankTransaction>> LoadPartnersOutsideAsync(FinanceTrackerContext context,
        TransferGroup group, List<BankTransaction> loaded, CancellationToken cancellationToken)
    {
        HashSet<Guid> loadedIds = loaded.Select(static x => x.Id).ToHashSet();

        List<Guid> partnerIds = loaded
            .Select(static x => x.Transfer?.PartnerTransactionId)
            .OfType<Guid>()
            .Where(x => !loadedIds.Contains(x))
            .Distinct()
            .ToList();

        return await group.Transactions(context)
            .Where(x => !loadedIds.Contains(x.Id) &&
                        (partnerIds.Contains(x.Id) ||
                         (x.Transfer != null && x.Transfer.PartnerTransactionId != null &&
                          loadedIds.Contains(x.Transfer.PartnerTransactionId.Value))))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Matches <paramref name="transactions"/>, the group's, saves the decisions and, with
    /// <paramref name="recordCheckpoint"/>, records that the user's run started from <paramref name="startedAt"/>.
    /// </summary>
    private async Task<TransferMatchSummary> MatchAndSaveAsync(FinanceTrackerContext context, TransferGroup group,
        List<BankTransaction> transactions, DateTime startedAt, bool recordCheckpoint,
        CancellationToken cancellationToken)
    {
        List<BankAccount> accounts = await group.Accounts(context).ToListAsync(cancellationToken);

        List<HouseholdMember> householdMembers =
            await context.HouseholdMembers.OwnedByAny(group.UserIds).ToListAsync(cancellationToken);

        Dictionary<Guid, IReadOnlyCollection<AccountIdentifier>> membersOwnAccounts =
            await MembersOwnAccountsAsync(context, householdMembers, cancellationToken);

        TransferMatchSummary summary = TransferMatcher.Match(accounts, householdMembers, transactions,
            _timeProvider.UtcNow, group.Pairing, membersOwnAccounts);

        if (recordCheckpoint)
        {
            await RecordCheckpointAsync(context, startedAt, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        Logger.LogMatchedTransfers(UserId, transactions.Count, summary.Transfers, summary.ToReview, summary.Cleared);
        CurrentActivity.Tag(
            (TelemetryTags.TransactionsConsidered, transactions.Count),
            (TelemetryTags.Transfers, summary.Transfers),
            (TelemetryTags.TransfersToReview, summary.ToReview),
            (TelemetryTags.TransfersCleared, summary.Cleared));
        _metrics.TransfersMatched(summary.Transfers, summary.ToReview, summary.Cleared);

        return summary;
    }

    /// <summary>
    /// The identifiers of the accounts each member who's a user connected themselves, by member, so money to or from
    /// them is recognised. Their own, whether or not they share them, as only the match is shown, never the account.
    /// </summary>
    private static async Task<Dictionary<Guid, IReadOnlyCollection<AccountIdentifier>>> MembersOwnAccountsAsync(
        FinanceTrackerContext context, List<HouseholdMember> householdMembers, CancellationToken cancellationToken)
    {
        List<Guid> linkedUserIds = householdMembers.Select(static x => x.LinkedUserId).OfType<Guid>().ToList();
        if (linkedUserIds.Count == 0)
        {
            return [];
        }

        List<BankAccount> theirs = await context.BankAccounts.AsNoTracking()
            .Where(x => linkedUserIds.Contains(x.UserId))
            .ToListAsync(cancellationToken);

        Dictionary<Guid, IReadOnlyCollection<AccountIdentifier>> byMember = [];
        foreach (HouseholdMember member in householdMembers.Where(static x => x.LinkedUserId is not null))
        {
            byMember[member.Id] = theirs
                .Where(x => x.UserId == member.LinkedUserId && x.Identifier is not null)
                .Select(static x => x.Identifier!)
                .ToList();
        }

        return byMember;
    }

    /// <summary>
    /// From when the run started rather than finished, so a transaction synced while it ran is picked up next time.
    /// </summary>
    private async Task RecordCheckpointAsync(FinanceTrackerContext context, DateTime startedAt,
        CancellationToken cancellationToken)
    {
        TransferMatchCheckpoint? checkpoint = await context.TransferMatchCheckpoints.OwnedBy(UserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (checkpoint is null)
        {
            context.TransferMatchCheckpoints.Add(new TransferMatchCheckpoint(UserId, startedAt));
        }
        else
        {
            checkpoint.Record(startedAt);
        }
    }
}
