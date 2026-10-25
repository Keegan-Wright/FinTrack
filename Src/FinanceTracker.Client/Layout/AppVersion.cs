using System.Reflection;

namespace FinanceTracker.Client.Layout;

/// <summary>
/// The running build's version, e.g <c>v0.1.1 · 3e871e2</c>.
///<remarks>
/// Release builds take it from the git tag, local dev from the Directory.Build.props
/// </remarks>
/// </summary>
public static class AppVersion
{
    private const int ShortCommitLength = 7;

    /// <summary>
    /// The informational version as built, e.g. <c>0.1.1+3e871e2…</c>.
    /// </summary>
    public static string Full { get; } =
        typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";

    public static string Display { get; } = Format(Full);

    internal static string Format(string informationalVersion)
    {
        string[] parts = informationalVersion.Split('+', 2);
        string version = $"v{parts[0]}";
        return parts.Length == 2 && parts[1].Length > 0
            ? $"{version} · {parts[1][..Math.Min(ShortCommitLength, parts[1].Length)]}"
            : version;
    }
}
