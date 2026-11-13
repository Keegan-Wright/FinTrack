namespace FinanceTracker.Contracts.Sharing;

/// <summary>
/// One of the user's own things they could share in a link, and how it's shared now.
/// </summary>
public sealed record ShareableItem
{
    /// <summary>
    /// What kind of thing it is.
    /// </summary>
    public required SharedItemKind Kind { get; init; }

    /// <summary>
    /// Its id, as its own feature knows it.
    /// </summary>
    public required Guid ItemId { get; init; }

    /// <summary>
    /// Its name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// How it's shared, null when it isn't.
    /// </summary>
    public ShareMode? Mode { get; init; }

    /// <summary>
    /// The shared account it goes with, when it's shared because that account is, such as a bill paid from it. It's
    /// shared as that account is, so it can't be chosen apart from it. Null when it's chosen on its own.
    /// </summary>
    public string? FollowsAccount { get; init; }
}
