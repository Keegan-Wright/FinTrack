namespace FinanceTracker.Domain.OpenBanking.Accounts;

/// <summary>
/// The kind of data a single sync pulled for an account.
/// </summary>
public enum SyncType
{
    /// <summary>
    /// The account's details.
    /// </summary>
    Account = 1,
    /// <summary>
    /// Its balance.
    /// </summary>
    Balance = 2,
    /// <summary>
    /// Its direct debits.
    /// </summary>
    DirectDebits = 3,
    /// <summary>
    /// Its standing orders.
    /// </summary>
    StandingOrders = 4,
    /// <summary>
    /// Its transactions.
    /// </summary>
    Transactions = 5
}
