using System.Diagnostics;

namespace FinanceTracker.Infrastructure.Telemetry;

/// <summary>
/// The spans the services publish, named with <see cref="TelemetryTags"/>. Exported because the app's tracing
/// listens to every source under its name, see the service defaults.
/// </summary>
public static class InfrastructureTelemetry
{
    /// <summary>
    /// Shared for the app's lifetime, as sources are meant to be.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new("FinanceTracker.Infrastructure");

    /// <summary>
    /// Tags the span this runs in with what the work did, such as a sync step's <c>Apply rules</c> span or the
    /// request that asked. Nothing when there's no span.
    /// </summary>
    public static void TagCurrent(params ReadOnlySpan<(string Key, object? Value)> tags)
    {
        Activity? current = Activity.Current;
        if (current is null)
        {
            return;
        }

        foreach ((string key, object? value) in tags)
        {
            current.SetTag(key, value);
        }
    }

    /// <summary>
    /// Adds one to a count on the span this runs in, starting it at one when the span has none yet.
    /// </summary>
    public static void IncrementCurrent(string key)
    {
        Activity? current = Activity.Current;
        if (current is null)
        {
            return;
        }

        int count = current.GetTagItem(key) as int? ?? 0;
        current.SetTag(key, count + 1);
    }
}
