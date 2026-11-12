using FinanceTracker.Domain.Bills;

namespace FinanceTracker.Infrastructure.Bills;

/// <summary>
/// Keeps the current user's bills up to date after a sync seeded from their standing orders and direct debits, see
/// <see cref="BillSeeder"/>, payments matched, see <see cref="BillMatcher"/>, and prices followed, see
/// <see cref="Bill.FollowPrice"/>, and subscriptions suggested, see <see cref="SubscriptionDetector"/>.
/// </summary>
public interface IBillSyncService
{
    /// <summary>
    /// Seeds and refreshes bills, then matches the last <see cref="BillSyncService.RecentDays"/> days of transactions
    /// to them, every transaction since it started for a bill new this run, then suggests the subscriptions left among
    /// them. Run after rules and transfers, so the matcher sees which bills rules said were paid.
    /// </summary>
    Task<BillSyncSummary> SyncAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Matches the transactions from <paramref name="from"/> on to the user's bills and follows prices, e.g. after a
    /// bill is added or changed, so its history shows straight away.
    /// </summary>
    /// <returns>How many transactions' bill changed.</returns>
    Task<int> MatchFromAsync(DateOnly from, CancellationToken cancellationToken);

    /// <summary>
    /// <see cref="MatchFromAsync"/> for <paramref name="ownerId"/>'s bills and transactions, after someone they share a
    /// bill with changed it.
    /// </summary>
    /// <returns>How many transactions' bill changed.</returns>
    Task<int> MatchFromForAsync(Guid ownerId, DateOnly from, CancellationToken cancellationToken);
}
