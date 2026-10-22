using System.ComponentModel;

namespace FinanceTracker.Contracts.Transactions;

/// <summary>
/// A transaction type the user can filter transactions by.
/// </summary>
public class TransactionTypeFilter
{
    /// <summary>
    /// The type, for <see cref="FilterTransactions.Types"/>.
    /// </summary>
    [Description("Type name for filtering transactions")]
    public required string TransactionType { get; init; }
}
