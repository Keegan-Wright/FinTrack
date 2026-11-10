using System.Diagnostics;

namespace FinanceTracker.Telemetry;

/// <summary>
/// Tags the span the calling code runs in, such as a sync step's <c>Apply rules</c> span or the request that asked,
/// with what the work did. Nothing when there's no span.
/// </summary>
public static class CurrentActivity
{
    public static void Tag(params ReadOnlySpan<(string Key, object? Value)> tags)
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
    /// Adds one to a count on the span, starting it at one when the span has none yet.
    /// </summary>
    public static void Increment(string key)
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
