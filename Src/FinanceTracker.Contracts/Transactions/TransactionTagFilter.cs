using System.ComponentModel;

namespace FinanceTracker.Contracts.Transactions;

/// <summary>
/// A tag the user can filter transactions by.
/// </summary>
public class TransactionTagFilter
{
    /// <summary>
    /// The tag, for <see cref="FilterTransactions.Tags"/>.
    /// </summary>
    [Description("Tag name for filtering transactions")]
    public required string Tag { get; init; }
}
