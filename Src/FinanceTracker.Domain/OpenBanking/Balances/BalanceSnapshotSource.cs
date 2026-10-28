namespace FinanceTracker.Domain.OpenBanking.Balances;

/// <summary>
/// Where a <see cref="BalanceSnapshot"/>'s figures came from.
/// </summary>
public enum BalanceSnapshotSource
{
    /// <summary>
    /// The bank's balance from the day's latest sync.
    /// </summary>
    Synced,

    /// <summary>
    /// Worked back from a later balance and the transactions since, so approximate.
    /// </summary>
    Derived
}
