namespace FinanceTracker.Telemetry;

/// <summary>
/// Why a single sign-on attempt didn't sign anyone in, on <see cref="FinTrackMetrics.SingleSignOnFailed"/>.
/// </summary>
public enum SingleSignOnFailure
{
    /// <summary>
    /// The sign in couldn't start: the provider refused this site, couldn't be reached, or its settings couldn't be
    /// read.
    /// </summary>
    NotStarted,

    /// <summary>
    /// It failed at the provider or on the way back, such as the person declining or a misconfigured client.
    /// </summary>
    Provider,

    /// <summary>
    /// The person came back without the identity the provider should have given them.
    /// </summary>
    NoIdentity,

    /// <summary>
    /// No account could be found for the identity, or made for it.
    /// </summary>
    NoAccount,

    /// <summary>
    /// The account is locked.
    /// </summary>
    LockedOut
}
