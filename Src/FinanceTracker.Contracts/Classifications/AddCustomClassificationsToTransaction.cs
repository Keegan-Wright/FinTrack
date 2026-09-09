using System.ComponentModel;

namespace FinanceTracker.Contracts.Classifications;

public class AddCustomClassificationsToTransaction
{
    [Description("Unique identifier of the transaction to classify")]
    public required Guid TransactionId { get; set; }

    [Description("Collection of classifications to apply to the transaction")]
    public required IEnumerable<SelectedCustomClassification> Classifications { get; set; }
}
