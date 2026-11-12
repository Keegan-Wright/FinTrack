namespace FinanceTracker.Contracts.Sharing;

/// <summary>
/// One of the other person's things they chose to share with the user in a link.
/// </summary>
public sealed record ItemSharedWithYou
{
    /// <summary>
    /// What kind of thing it is.
    /// </summary>
    public required SharedItemKind Kind { get; init; }

    /// <summary>
    /// Its name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// What the user can do with it.
    /// </summary>
    public required ShareMode Mode { get; init; }
}
