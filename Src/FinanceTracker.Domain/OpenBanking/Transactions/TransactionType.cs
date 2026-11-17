namespace FinanceTracker.Domain.OpenBanking.Transactions;

/// <summary>
/// Which way the money went.
/// </summary>
public enum TransactionType
{
    /// <summary>
    /// Money out.
    /// </summary>
    Debit = 1,
    /// <summary>
    /// Money in.
    /// </summary>
    Credit = 2
}
