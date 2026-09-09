using System.ComponentModel;

namespace FinanceTracker.Contracts.Classifications;

public class Classification
{
    [Description("Tag or label associated with the classification")]
    public string? Tag { get; init; }

    [Description("Unique identifier for the classification")]
    public Guid ClassificationId { get; init; }
}
