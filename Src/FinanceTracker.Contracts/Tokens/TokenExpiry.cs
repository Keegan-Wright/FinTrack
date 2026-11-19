namespace FinanceTracker.Contracts.Tokens;

/// <summary>
/// How long a new personal access token lasts.
/// </summary>
public enum TokenExpiry
{
    /// <summary>
    /// 30 days.
    /// </summary>
    ThirtyDays,

    /// <summary>
    /// 90 days.
    /// </summary>
    NinetyDays,

    /// <summary>
    /// A year.
    /// </summary>
    OneYear,

    /// <summary>
    /// Until it's revoked. The user looks after revoking and replacing it if it leaks.
    /// </summary>
    Never
}
