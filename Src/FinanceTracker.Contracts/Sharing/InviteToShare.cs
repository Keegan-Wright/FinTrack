namespace FinanceTracker.Contracts.Sharing;

/// <summary>
/// Invites another user on the instance to share, found by their username or email address.
/// </summary>
public sealed record InviteToShare
{
    /// <summary>
    /// Their username or email address, in any case.
    /// </summary>
    public required string Who { get; init; }
}
