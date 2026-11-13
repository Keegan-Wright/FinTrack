namespace FinanceTracker.Contracts.Sharing;

/// <summary>
/// One of the user's things to share in a link, and what the other person can do with it.
/// </summary>
public sealed record ItemShareChoice
{
    /// <summary>
    /// What kind of thing it is.
    /// </summary>
    public required SharedItemKind Kind { get; init; }

    /// <summary>
    /// Its id, see <see cref="ShareableItem.ItemId"/>.
    /// </summary>
    public required Guid ItemId { get; init; }

    /// <summary>
    /// What the other person can do with it.
    /// </summary>
    public required ShareMode Mode { get; init; }
}
