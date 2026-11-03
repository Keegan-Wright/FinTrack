using FinanceTracker.Domain.NetWorth;
using FinanceTracker.Domain.OpenBanking.Balances;

namespace FinanceTracker.Infrastructure.OpenBanking.Balances;

/// <summary>
/// Keeps the current user's balance history.
/// </summary>
public interface IBalanceSnapshotService
{
    /// <summary>
    /// Updates each account's <see cref="BalanceSnapshot"/> for <paramref name="day"/> from its stored balance, then
    /// re-totals the day's <see cref="NetWorthSnapshot"/>. Each sync replaces the last, so once the day is over its
    /// snapshots hold the balances from its last sync.
    /// </summary>
    /// <param name="balancesSyncedFrom">
    /// When the sync started. An account whose balance hasn't synced since keeps the day's snapshot it already has,
    /// or gets a stale one of its last stored balance if the day doesn't have one yet.
    /// </param>
    /// <returns>The day's net worth, or null when no account has a balance yet.</returns>
    Task<NetWorthSnapshot?> SnapshotAsync(DateOnly day, DateTime balancesSyncedFrom,
        CancellationToken cancellationToken);

    /// <summary>
    /// Works out past closing balances for the accounts that haven't had it done yet, see
    /// <see cref="BalanceHistory"/>, then re-totals the net worth of each day that gained one. Once per account,
    /// after the sync that gives it a balance.
    /// </summary>
    /// <returns>How many days of balances were added, across the accounts.</returns>
    Task<int> BackfillAsync(CancellationToken cancellationToken);
}
