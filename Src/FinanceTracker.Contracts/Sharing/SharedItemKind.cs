namespace FinanceTracker.Contracts.Sharing;

/// <summary>
/// The kinds of thing besides accounts a user can choose to share in a link.
/// </summary>
public enum SharedItemKind
{
    /// <summary>
    /// A tag. Shared, the other person can put it on their own transactions too, and rename it when allowed changes.
    /// </summary>
    Classification = 0
}
