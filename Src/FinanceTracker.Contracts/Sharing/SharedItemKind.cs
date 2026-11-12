namespace FinanceTracker.Contracts.Sharing;

/// <summary>
/// The kinds of thing besides accounts a user can choose to share in a link.
/// </summary>
public enum SharedItemKind
{
    /// <summary>
    /// A tag. Shared, the other person can put it on their own transactions too, and rename it when allowed changes.
    /// </summary>
    Classification = 0,

    /// <summary>
    /// A rule. Shared, the other person sees it on their Rules page, and edits it too when allowed changes. It still
    /// runs where its owner's rules run.
    /// </summary>
    Rule = 1,

    /// <summary>
    /// A bill. One paid from an account shared with the other person is shared with it, in the same way. Others can be
    /// chosen. Shared, the other person sees it, and edits it too when allowed changes.
    /// </summary>
    Bill = 2
}
