using System.ComponentModel;

namespace FinanceTracker.Models.Response.Transaction;

public class TransactionProviderFilterResponse
{
    [Description("Unique identifier of the transaction provider")]
    public required Guid ProviderId { get; init; }

    [Description("Name of the transaction provider")]
    public required string ProviderName { get; init; }
}
