using System.ComponentModel;

namespace FinanceTracker.Contracts.NetWorth;

/// <summary>
/// One account's closing balances over time.
/// </summary>
public class AccountBalanceHistory
{
    /// <summary>
    /// The account.
    /// </summary>
    [Description("The account's id")]
    public Guid AccountId { get; init; }

    /// <summary>
    /// Its closing balances, oldest first, one per day it has one.
    /// </summary>
    [Description("The account's closing balances, oldest first")]
    public List<BalancePoint> Points { get; init; } = [];
}
