namespace FinanceTracker.Contracts.Tokens;

/// <summary>
/// One of the user's personal access tokens, as their API tokens page lists them. The token itself is never shown
/// again after it's made.
/// </summary>
public sealed class TokenSummary
{
    /// <summary>
    /// The token's id, to revoke it by.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// What it's for, e.g. <c>Home Assistant</c>.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// When it was made, in UTC.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// When it stops working, in UTC, null for never.
    /// </summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    /// When it was last used, in UTC, to the nearest minute. Null before its first use.
    /// </summary>
    public DateTime? LastUsedAt { get; init; }

    /// <summary>
    /// Whether it's expired, so no longer works.
    /// </summary>
    public required bool Expired { get; init; }
#if FINTRACK_AI

    /// <summary>
    /// Whether it's for AI clients through MCP. Only in the AI build.
    /// </summary>
    public bool ForAiClients { get; init; }
#endif
}
