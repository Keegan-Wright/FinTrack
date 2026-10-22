using System.ComponentModel;

namespace FinanceTracker.Contracts.Classifications;

/// <summary>
/// One of the user's custom classifications, looked up by id.
/// </summary>
public class GetClassification
{
    /// <summary>
    /// The classification's id.
    /// </summary>
    [Description("Unique identifier for the classification")]
    public required Guid ClassificationId { get; init; }

    /// <summary>
    /// The classification's name.
    /// </summary>
    [Description("Tag or label associated with the classification")]
    public required string Tag { get; init; }
}
