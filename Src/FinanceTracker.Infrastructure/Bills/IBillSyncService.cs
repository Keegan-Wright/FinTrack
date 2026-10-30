using FinanceTracker.Domain.Bills;

namespace FinanceTracker.Infrastructure.Bills;

/// <summary>
/// Keeps the current user's bills up to date after a sync seeded from their standing orders and direct debits, see
/// <see cref="BillSeeder"/>, payments matched, see <see cref="BillMatcher"/>, and prices followed, see
/// <see cref="Bill.FollowPrice"/>.
/// </summary>
public interface IBillSyncService
{
    /// <summary>
    /// Seeds and refreshes bills, then matches the last <see cref="BillSyncService.RecentDays"/> days of transactions
    /// to them, every transaction since it started for a bill new this run. Run after rules and transfers, so the
    /// matcher sees which bills rules said were paid.
    /// </summary>
    Task<BillSyncSummary> SyncAsync(CancellationToken cancellationToken);
}
