namespace FinanceTracker.Domain.OpenBanking.Accounts;

/// <summary>
/// The kind of data a single sync pulled for an account.
/// </summary>
public enum SyncType
{
    Account = 1,
    Balance = 2,
    DirectDebits = 3,
    StandingOrders = 4,
    Transactions = 5,
    PendingTransactions = 6
}
