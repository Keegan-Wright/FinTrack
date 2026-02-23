using System.ComponentModel;

namespace FinanceTracker.Models.Response.Classifications;

public class ClassificationsResponse
{
    [Description("Tag or label associated with the classification")]
    public string? Tag { get; init; }

    [Description("Unique identifier for the classification")]
    public Guid ClassificationId { get; init; }
}
