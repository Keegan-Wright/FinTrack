using System.ComponentModel;

namespace FinanceTracker.Models.Request.Classifications;

public class SelectedCustomClassificationsRequest
{
    [Description("Unique identifier of the classification to apply")]
    public required Guid ClassificationId { get; set; }
}
