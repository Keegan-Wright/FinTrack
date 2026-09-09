using System.ComponentModel;

namespace FinanceTracker.Contracts.Classifications;

public class SelectedCustomClassificationsRequest
{
    [Description("Unique identifier of the classification to apply")]
    public required Guid ClassificationId { get; init; }
}
