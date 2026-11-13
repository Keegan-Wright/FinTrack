namespace FinanceTracker.Contracts.Sharing;

/// <summary>
/// Merges a joint account both people connected, from a <see cref="JointAccountSuggestion"/>.
/// </summary>
public sealed record MergeJointAccount
{
    /// <summary>
    /// The user's copy.
    /// </summary>
    public required Guid YourAccountId { get; init; }

    /// <summary>
    /// The other person's copy.
    /// </summary>
    public required Guid TheirAccountId { get; init; }
}
