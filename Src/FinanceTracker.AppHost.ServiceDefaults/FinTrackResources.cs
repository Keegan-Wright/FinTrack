namespace FinanceTracker.AppHost.ServiceDefaults;

/// <summary>
/// Aspire resource names, shared so the AppHost, the web app and the integration tests agree on them. Connection
/// strings are looked up by these names.
/// </summary>
public static class FinTrackResources
{
    public const string Web = "FinTrackWeb";
    public const string Postgres = "FinTrack-Postgres";
    public const string Database = "FinTrackDb";
    public const string Redis = "FinTrack-Redis";
}
