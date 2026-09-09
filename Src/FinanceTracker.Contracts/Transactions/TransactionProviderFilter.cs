using System.ComponentModel;

namespace FinanceTracker.Contracts.Transactions;

public class TransactionProviderFilter
{
    [Description("Unique identifier of the transaction provider")]
    public required Guid ProviderId { get; init; }

    [Description("Name of the transaction provider")]
    public required string ProviderName { get; init; }
}
