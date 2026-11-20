namespace FinanceTracker.Tokens;

/// <summary>
/// How many refused personal access tokens <see cref="TokenFailureLimiter"/> lets an address have, from
/// <c>RATE_LIMIT_TOKEN_FAILURES_PER_HOUR</c>.
/// </summary>
public sealed class TokenFailureLimiterOptions
{
    /// <summary>
    /// Tokens one address may have refused an hour, when not set.
    /// </summary>
    public const int DefaultFailuresPerHour = 30;

    /// <summary>
    /// Tokens one address may have refused an hour before its tokens aren't looked at.
    /// </summary>
    public int FailuresPerHour { get; set; } = DefaultFailuresPerHour;
}
