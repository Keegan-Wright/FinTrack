using FinanceTracker.Domain.Household;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.OpenBanking.Transfers;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<ITransferMatchingService>]
public class TransferMatchingService : ServiceBase<TransferMatchingService>, ITransferMatchingService
{
    private readonly TimeProvider _timeProvider;
    private DateTime Now => _timeProvider.GetUtcNow().UtcDateTime;

    public TransferMatchingService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<TransferMatchingService> logger,
        TimeProvider timeProvider) : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
    }

    public async Task<TransferMatchSummary> MatchAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        DateTime startedAt = Now;

        // Amounts and dates are encrypted, so matching happens in memory. Tracked, so saving writes only the
        // transactions the matcher changed
        List<BankTransaction> transactions =
            await context.BankTransactions.OwnedBy(UserId).ToListAsync(cancellationToken);

        return await MatchAndSaveAsync(context, transactions, startedAt, recordCheckpoint: true, cancellationToken);
    }

    public async Task<TransferMatchSummary?> MatchChangesAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        DateTime startedAt = Now;

        TransferMatchCheckpoint? checkpoint = await context.TransferMatchCheckpoints.OwnedBy(UserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (checkpoint is null)
        {
            List<BankTransaction> everything =
                await context.BankTransactions.OwnedBy(UserId).ToListAsync(cancellationToken);
            return await MatchAndSaveAsync(context, everything, startedAt, recordCheckpoint: true, cancellationToken);
        }

        DateTime since = checkpoint.MatchedFrom;
        List<DateTime> changed = await context.BankTransactions.OwnedBy(UserId)
            .Where(x => x.Created > since || x.Updated > since)
            .Select(static x => x.OccurredAt)
            .ToListAsync(cancellationToken);

        if (changed.Count == 0)
        {
            Logger.LogNoTransferChanges(UserId);
            return null;
        }

        return await MatchAroundAsync(context, changed, startedAt, recordCheckpoint: true, cancellationToken);
    }

    public async Task<TransferMatchSummary> MatchAroundAsync(IReadOnlyCollection<Guid> transactionIds,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DateTime startedAt = Now;

        List<DateTime> dates = await context.BankTransactions.OwnedBy(UserId)
            .Where(x => transactionIds.Contains(x.Id))
            .Select(static x => x.OccurredAt)
            .ToListAsync(cancellationToken);

        if (dates.Count == 0)
        {
            return new TransferMatchSummary(0, 0, 0);
        }

        // A sync saved since the last run hasn't been matched yet, so the checkpoint stays put for that run to find it
        return await MatchAroundAsync(context, dates, startedAt, recordCheckpoint: false, cancellationToken);
    }

    /// <summary>
    /// Matches the transactions near <paramref name="dates"/>. Something on those dates can only affect transactions
    /// close enough to be its mirror, so the window runs that far either side, and up to now as late ones are the
    /// likeliest to change.
    /// </summary>
    private async Task<TransferMatchSummary> MatchAroundAsync(FinanceTrackerContext context, List<DateTime> dates,
        DateTime startedAt, bool recordCheckpoint, CancellationToken cancellationToken)
    {
        DateTime from = dates.Min().AddDays(-TransferMatcher.MaxDaysApart);
        DateTime to = (dates.Max() > startedAt ? dates.Max() : startedAt).AddDays(TransferMatcher.MaxDaysApart);

        List<BankTransaction> transactions = await context.BankTransactions.OwnedBy(UserId)
            .WhereEncryptedDayBetween(x => x.OccurredAt, from, to, context)
            .ToListAsync(cancellationToken);

        transactions.AddRange(await LoadPartnersOutsideAsync(context, transactions, cancellationToken));

        return await MatchAndSaveAsync(context, transactions, startedAt, recordCheckpoint, cancellationToken);
    }

    /// <summary>
    /// The transactions paired with <paramref name="loaded"/> that it doesn't include, either way round. The matcher
    /// treats what it's given as the whole picture, without them it would clear a match whose other half is outside
    /// the window, and could pair a user's confirmed partner with something else.
    /// </summary>
    private async Task<List<BankTransaction>> LoadPartnersOutsideAsync(FinanceTrackerContext context,
        List<BankTransaction> loaded, CancellationToken cancellationToken)
    {
        HashSet<Guid> loadedIds = loaded.Select(static x => x.Id).ToHashSet();

        List<Guid> partnerIds = loaded
            .Select(static x => x.Transfer?.PartnerTransactionId)
            .OfType<Guid>()
            .Where(x => !loadedIds.Contains(x))
            .Distinct()
            .ToList();

        return await context.BankTransactions.OwnedBy(UserId)
            .Where(x => !loadedIds.Contains(x.Id) &&
                        (partnerIds.Contains(x.Id) ||
                         (x.Transfer != null && x.Transfer.PartnerTransactionId != null &&
                          loadedIds.Contains(x.Transfer.PartnerTransactionId.Value))))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Matches <paramref name="transactions"/>, saves the decisions and, with <paramref name="recordCheckpoint"/>,
    /// records that it ran from <paramref name="startedAt"/>.
    /// </summary>
    private async Task<TransferMatchSummary> MatchAndSaveAsync(FinanceTrackerContext context,
        List<BankTransaction> transactions, DateTime startedAt, bool recordCheckpoint,
        CancellationToken cancellationToken)
    {
        List<BankAccount> accounts = await context.BankAccounts.OwnedBy(UserId).ToListAsync(cancellationToken);

        List<HouseholdMember> householdMembers =
            await context.HouseholdMembers.OwnedBy(UserId).ToListAsync(cancellationToken);

        TransferMatchSummary summary = TransferMatcher.Match(accounts, householdMembers, transactions, Now);

        if (recordCheckpoint)
        {
            await RecordCheckpointAsync(context, startedAt, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        Logger.LogMatchedTransfers(UserId, transactions.Count, summary.Transfers, summary.ToReview, summary.Cleared);

        return summary;
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
