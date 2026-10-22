using System.Diagnostics;

namespace FinanceTracker;

internal static class Telemetry
{
    /// <summary>
    /// Shared for the app's lifetime, as sources are meant to be. The name matches the application name that
    /// ServiceDefaults registers for tracing, so these activities are exported.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new("FinanceTracker");
}
