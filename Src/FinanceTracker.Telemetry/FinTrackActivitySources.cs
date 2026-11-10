using System.Diagnostics;

namespace FinanceTracker.Telemetry;

/// <summary>
/// The activity sources FinTrack publishes its own spans through, shared for the app's lifetime as sources are meant
/// to be. The service defaults listen to each by name, see <see cref="All"/>.
/// </summary>
public static class FinTrackActivitySources
{
    /// <summary>
    /// The web app's spans, such as the background jobs'.
    /// </summary>
    public const string AppName = "FinanceTracker";

    /// <summary>
    /// The services' spans, such as a connection's sync.
    /// </summary>
    public const string InfrastructureName = "FinanceTracker.Infrastructure";

    public static readonly ActivitySource App = new(AppName);

    public static readonly ActivitySource Infrastructure = new(InfrastructureName);

    /// <summary>
    /// Every source above by name, for tracing to listen to.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [AppName, InfrastructureName];
}
