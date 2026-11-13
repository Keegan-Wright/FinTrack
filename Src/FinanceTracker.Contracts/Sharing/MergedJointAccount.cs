namespace FinanceTracker.Contracts.Sharing;

/// <summary>
/// A joint account both people connected, merged so it counts once.
/// </summary>
public sealed record MergedJointAccount
{
    /// <summary>
    /// The merge's id, to split it again.
    /// </summary>
    public required Guid JointAccountId { get; init; }

    /// <summary>
    /// The name of the copy both of them see.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Whether the copy both see is the user's, rather than the other person's.
    /// </summary>
    public required bool ShownIsYours { get; init; }
}
