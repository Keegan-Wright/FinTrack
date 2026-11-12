using System.ComponentModel;

namespace FinanceTracker.Contracts.Classifications;

/// <summary>
/// One of the user's custom classifications, or one another person shares with them.
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

    /// <summary>
    /// The first and last name of whoever shares it with the user, null when it's their own.
    /// </summary>
    [Description("Who shares the classification with the user, empty when it's their own")]
    public string? SharedBy { get; init; }

    /// <summary>
    /// Whether the user can rename it: their own, or shared with them with changes allowed. Only its owner deletes it.
    /// </summary>
    [Description("Whether the user can rename the classification")]
    public bool CanChange { get; init; } = true;
}
