using FinanceTracker.Domain.PayPeriods;

namespace FinanceTracker.Infrastructure.PayPeriods;

/// <summary>
/// Keeps the current user's pay up to date after a sync, see <see cref="PayDetector"/> and
/// <see cref="PaySchedule.Update"/>.
/// </summary>
public interface IPaySyncService
{
    /// <summary>
    /// Picks up the user's pay from their transactions, or the pay days since the last sync. Run after transfers, so
    /// money moved from the user's own accounts isn't taken as pay.
    /// </summary>
    /// <returns>Whether the user has a pay to go by.</returns>
    Task<bool> SyncAsync(CancellationToken cancellationToken);
}
