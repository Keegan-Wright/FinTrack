namespace FinanceTracker.Contracts.Sharing;

/// <summary>
/// Chooses Everyone or Just mine for the money views.
/// </summary>
public sealed record ChangeSharingView
{
    /// <summary>
    /// Show accounts shared with them beside their own.
    /// </summary>
    public required bool IncludeShared { get; init; }
}
