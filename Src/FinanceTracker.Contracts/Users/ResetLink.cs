namespace FinanceTracker.Contracts.Users;

/// <summary>
/// A one-time link that lets a user choose a new password, for an admin to hand them when FinTrack can't email it.
/// </summary>
public sealed class ResetLink
{
    /// <summary>
    /// The link, whole, to send them.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// When it stops working, in UTC, unless it's used before then.
    /// </summary>
    public required DateTime ExpiresAt { get; init; }
}
