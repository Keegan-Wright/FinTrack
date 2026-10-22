using System.ComponentModel;

namespace FinanceTracker.Contracts.Transactions;

/// <summary>
/// An account the user can filter transactions by.
/// </summary>
public class TransactionAccountFilter
{
    /// <summary>
    /// The account's id, for <see cref="FilterTransactions.AccountIds"/>.
    /// </summary>
    [Description("Unique identifier of the account")]
    public required Guid AccountId { get; init; }

    /// <summary>
    /// The account's name.
    /// </summary>
    [Description("Name of the account")]
    public required string AccountName { get; init; }
}
