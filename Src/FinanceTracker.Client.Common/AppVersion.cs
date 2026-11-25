using System.Reflection;

namespace FinanceTracker.Client.Common;

/// <summary>
/// The running build's version, e.g <c>v0.1.1 · 3e871e2</c>.
///<remarks>
/// Release builds take it from the git tag, local dev from the Directory.Build.props
/// </remarks>
/// </summary>
public static class AppVersion
{
    /// <summary>
    /// How much of the commit is shown.
    /// </summary>
    private const int ShortCommitLength = 7;

    /// <summary>
    /// The informational version as built, e.g. <c>0.1.1+3e871e2…</c>.
    /// </summary>
    public static string Full { get; } =
        typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";

    /// <summary>
    /// The version as shown in the app, see <see cref="Format"/>.
    /// </summary>
    public static string Display { get; } = Format(Full);

    /// <summary>
    /// The version and the start of its commit, e.g. <c>v0.1.1 · 3e871e2</c>.
    /// </summary>
    /// <param name="informationalVersion">The informational version as built.</param>
    internal static string Format(string informationalVersion)
    {
        string[] parts = informationalVersion.Split('+', 2);
        string version = $"v{parts[0]}";
        return parts.Length == 2 && parts[1].Length > 0
            ? $"{version} · {parts[1][..Math.Min(ShortCommitLength, parts[1].Length)]}"
            : version;
    }
}
