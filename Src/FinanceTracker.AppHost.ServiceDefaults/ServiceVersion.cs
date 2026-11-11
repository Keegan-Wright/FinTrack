using System.Reflection;

namespace FinanceTracker.AppHost.ServiceDefaults;

/// <summary>
/// The version telemetry goes out under as <c>service.version</c>, e.g. <c>v0.1.6</c>, so dashboards can mark when
/// each release went out. Release builds take it from the git tag, see Src/Dockerfile, anything else is
/// <c>v0.0.0-dev</c>.
/// </summary>
public static class ServiceVersion
{
    /// <summary>
    /// The running app's version, read from the app's own assembly.
    /// </summary>
    public static string Current { get; } = From(Assembly.GetEntryAssembly()
        ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    /// <summary>
    /// <paramref name="informationalVersion"/> without the commit the SDK adds after a <c>+</c>, which would make
    /// every build its own release.
    /// </summary>
    public static string From(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return "unknown";
        }

        string version = informationalVersion.Split('+', 2)[0];
        return $"v{version}";
    }
}
