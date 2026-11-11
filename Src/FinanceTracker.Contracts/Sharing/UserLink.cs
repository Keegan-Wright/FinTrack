namespace FinanceTracker.Contracts.Sharing;

/// <summary>
/// Someone the signed in user is linked with, or has an invite out with either way.
/// </summary>
public sealed record UserLink
{
    /// <summary>
    /// The link's id, for answering or ending it.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The other person's first and last name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The other person's username, which tells two people of the same name apart.
    /// </summary>
    public required string Username { get; init; }

    /// <summary>
    /// Whether they're linked, or which way an invite is waiting.
    /// </summary>
    public required LinkState State { get; init; }

    /// <summary>
    /// When the invite was sent, or when it was accepted once they're linked.
    /// </summary>
    public required DateTime Since { get; init; }
}
