using System.Diagnostics;
using FinanceTracker.Infrastructure.Telemetry;

namespace FinanceTracker;

internal static class Telemetry
{
    /// <summary>
    /// Shared for the app's lifetime, as sources are meant to be. The name matches the application name that
    /// ServiceDefaults registers for tracing, so these activities are exported.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new("FinanceTracker");

    /// <summary>
    /// A span for work done for <paramref name="userId"/>, a child of the current one, tagged with who it's for.
    /// </summary>
    public static Activity? StartUserActivity(string name, Guid userId)
    {
        Activity? activity = ActivitySource.StartActivity(name);
        activity?.SetTag(TelemetryTags.UserId, userId.ToString());
        return activity;
    }
}
