namespace FinanceTracker.AppHost.ServiceDefaults;

/// <summary>
/// How a request moves the app's clock once <see cref="FinTrackEnvironment.TestClock"/> is on, shared so the web app
/// and the integration tests agree on it.
/// </summary>
public static class TestClock
{
    /// <summary>
    /// The cookie holding how far the request's clock is from the real one, in whole seconds, e.g. <c>-86400</c> for
    /// a day earlier. Time still passes as it does, only from somewhere else.
    /// </summary>
    public const string Cookie = "FinTrack-Test-Clock-Offset";
}
