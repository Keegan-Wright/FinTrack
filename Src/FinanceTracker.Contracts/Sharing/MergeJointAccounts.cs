namespace FinanceTracker.Contracts.Sharing;

/// <summary>
/// Merges joint accounts both people connected, one or several of the link's
/// <see cref="LinkShares.JointSuggestions"/> at once.
/// </summary>
public sealed record MergeJointAccounts
{
    /// <summary>
    /// Each pair to merge, the user's copy and the other person's.
    /// </summary>
    public required IReadOnlyList<MergeJointAccount> Accounts { get; init; }
}
