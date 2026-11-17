namespace FinanceTracker.AppHost.ServiceDefaults;

/// <summary>
/// Aspire resource names, shared so the AppHost, the web app and the integration tests agree on them. Connection
/// strings are looked up by these names.
/// </summary>
public static class FinTrackResources
{
    /// <summary>
    /// The web app.
    /// </summary>
    public const string Web = "FinTrack";
    /// <summary>
    /// The Postgres server.
    /// </summary>
    public const string Postgres = "FinTrack-Postgres";
    /// <summary>
    /// The app's database on the Postgres server.
    /// </summary>
    public const string Database = "FinTrackDb";

    /// <summary>
    /// The mail catcher notification emails go to while developing and in the tests.
    /// </summary>
    public const string Mail = "FinTrack-Mail";
}
