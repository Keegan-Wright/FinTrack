using System.ComponentModel;

namespace FinanceTracker.Contracts.Classifications;

public class SelectedCustomClassification
{
    [Description("Unique identifier of the classification to apply")]
    public required Guid ClassificationId { get; init; }
}
