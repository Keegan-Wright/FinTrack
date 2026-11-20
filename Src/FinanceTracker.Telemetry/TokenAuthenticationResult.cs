namespace FinanceTracker.Telemetry;

/// <summary>
/// How a request with a personal access token went, on <see cref="FinTrackMetrics.TokenAuthenticated"/>.
/// </summary>
public enum TokenAuthenticationResult
{
    /// <summary>
    /// The token was good, so the request was signed in as its user.
    /// </summary>
    Succeeded,

    /// <summary>
    /// It wasn't in FinTrack's token format.
    /// </summary>
    Malformed,

    /// <summary>
    /// No token has that id and secret, such as a guess or a typo.
    /// </summary>
    Unknown,

    /// <summary>
    /// The token has expired or been revoked.
    /// </summary>
    Inactive,

    /// <summary>
    /// The token's user is locked.
    /// </summary>
    Locked,

    /// <summary>
    /// Its address has had too many tokens refused lately, so it wasn't looked at.
    /// </summary>
    RateLimited
}
