using System.ComponentModel;

namespace FinanceTracker.Contracts.Transactions;

/// <summary>
/// A bank connection the user can filter transactions by.
/// </summary>
public class TransactionProviderFilter
{
    /// <summary>
    /// The connection's id (not the open banking provider's bank id), for <see cref="FilterTransactions.ProviderIds"/>.
    /// </summary>
    [Description("Unique identifier of the transaction provider")]
    public required Guid ProviderId { get; init; }

    /// <summary>
    /// The bank's name.
    /// </summary>
    [Description("Name of the transaction provider")]
    public required string ProviderName { get; init; }
}
