using System.ComponentModel;

namespace FinanceTracker.Contracts.Classifications;

/// <summary>
/// One of the user's custom classifications.
/// </summary>
public class Classification
{
    /// <summary>
    /// The classification's name.
    /// </summary>
    [Description("Tag or label associated with the classification")]
    public string? Tag { get; init; }

    /// <summary>
    /// The classification's id.
    /// </summary>
    [Description("Unique identifier for the classification")]
    public Guid ClassificationId { get; init; }
}
