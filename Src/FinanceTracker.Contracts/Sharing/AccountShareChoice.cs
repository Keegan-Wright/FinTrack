namespace FinanceTracker.Contracts.Sharing;

/// <summary>
/// One account to share, and how.
/// </summary>
public sealed record AccountShareChoice
{
    /// <summary>
    /// One of the user's own accounts.
    /// </summary>
    public required Guid AccountId { get; init; }

    /// <summary>
    /// What the other person can do with it.
    /// </summary>
    public required ShareMode Mode { get; init; }
}
