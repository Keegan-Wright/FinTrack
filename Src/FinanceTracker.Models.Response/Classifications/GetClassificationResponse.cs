using System.ComponentModel;

namespace FinanceTracker.Models.Response.Classifications;

public class GetClassificationResponse
{
    [Description("Unique identifier for the classification")]
    public required Guid ClassificationId { get; init; }

    [Description("Tag or label associated with the classification")]
    public required string Tag { get; init; }
}
