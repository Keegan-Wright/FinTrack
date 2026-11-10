using System.Diagnostics;

namespace FinanceTracker.Infrastructure.Telemetry;

/// <summary>
/// The spans the services publish, and the attribute names they share. Exported because the app's tracing listens
/// to every source under its name, see the service defaults.
/// </summary>
public static class InfrastructureTelemetry
{
    /// <summary>
    /// The span attribute naming the user the work is for, OpenTelemetry's <c>user.id</c>.
    /// </summary>
    public const string UserIdTag = "user.id";

    /// <summary>
    /// The bank connection a span is for.
    /// </summary>
    public const string ConnectionIdTag = "fintrack.connection.id";

    /// <summary>
    /// The account or card a span is for.
    /// </summary>
    public const string AccountIdTag = "fintrack.account.id";

    /// <summary>
    /// How many items a fetch from the bank returned.
    /// </summary>
    public const string ItemCountTag = "fintrack.items";

    /// <summary>
    /// TrueLayer's id for a request it refused, which their support can look up.
    /// </summary>
    public const string TrueLayerTraceIdTag = "truelayer.trace_id";

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
}
