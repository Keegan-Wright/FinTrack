namespace FinanceTracker.Contracts.Api;

/// <summary>
/// Headers the API requires, and those it answers with.
/// </summary>
public static class ApiHeaders
{
    /// <summary>
    /// Every API request must carry this header, with any value, or it's rejected with 400.
    /// </summary>
    /// <remarks>
    /// The API signs users in with the same cookie as the pages, so it needs protecting from cross site requests. A
    /// browser only sends a custom header cross origin after a CORS preflight the app never allows, so a request
    /// carrying it came from the app's own pages.
    /// </remarks>
    public const string Request = "X-FinTrack-Request";

    /// <summary>
    /// Every API answer carries the build of the app the server runs, e.g. <c>0.1.1+3e871e2…</c>, so a browser tab
    /// still running the app from before an update can tell.
    /// </summary>
    public const string Version = "X-FinTrack-Version";
}
