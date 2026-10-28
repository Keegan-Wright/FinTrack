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

        return await MatchAndSaveAsync(context, transactions, startedAt, cancellationToken);
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
            return await MatchAndSaveAsync(context, everything, startedAt, cancellationToken);
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

        // A change can only affect transactions close enough to be its mirror, so the window runs that far either side
        // of the changes, and up to now as late ones are the likeliest to change
        DateTime from = changed.Min().AddDays(-TransferMatcher.MaxDaysApart);
        DateTime to = (changed.Max() > startedAt ? changed.Max() : startedAt).AddDays(TransferMatcher.MaxDaysApart);

        List<BankTransaction> transactions = await context.BankTransactions.OwnedBy(UserId)
            .WhereEncryptedDayBetween(x => x.OccurredAt, from, to, context)
            .ToListAsync(cancellationToken);

        transactions.AddRange(await LoadPartnersOutsideAsync(context, transactions, cancellationToken));

        return await MatchAndSaveAsync(context, transactions, startedAt, cancellationToken);
    }

    private DateTime Now => _timeProvider.GetUtcNow().UtcDateTime;

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
    /// Matches <paramref name="transactions"/>, saves the decisions and records that it ran from
    /// <paramref name="startedAt"/>.
    /// </summary>
    private async Task<TransferMatchSummary> MatchAndSaveAsync(FinanceTrackerContext context,
        List<BankTransaction> transactions, DateTime startedAt, CancellationToken cancellationToken)
    {
        List<BankAccount> accounts = await context.BankAccounts.OwnedBy(UserId).ToListAsync(cancellationToken);

        List<HouseholdMember> householdMembers =
            await context.HouseholdMembers.OwnedBy(UserId).ToListAsync(cancellationToken);

        TransferMatchSummary summary = TransferMatcher.Match(accounts, householdMembers, transactions, Now);

        // From when it started rather than finished, so a transaction synced while it ran is picked up next time
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

        await context.SaveChangesAsync(cancellationToken);
        Logger.LogMatchedTransfers(UserId, transactions.Count, summary.Transfers, summary.ToReview, summary.Cleared);

        return summary;
    }
}
