using System.ComponentModel;

namespace FinanceTracker.Models.Request.Classifications;

public class AddCustomClassificationsToTransactionRequest
{
    [Description("Unique identifier of the transaction to classify")]
    public required Guid TransactionId { get; init; }

    [Description("Collection of classifications to apply to the transaction")]
    public required IEnumerable<SelectedCustomClassificationsRequest> Classifications { get; init; }
}
