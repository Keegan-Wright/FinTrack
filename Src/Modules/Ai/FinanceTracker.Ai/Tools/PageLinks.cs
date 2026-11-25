using Microsoft.Extensions.Configuration;

namespace FinanceTracker.Ai.Tools;

/// <summary>
/// Turns a page's path into the link a tool's result carries, a full address when the install's public address is
/// known, so an AI client outside FinTrack can open it.
/// </summary>
/// <param name="configuration">The app's settings, for <see cref="PublicUrlVariable"/>.</param>
public sealed class PageLinks(IConfiguration configuration)
{
    /// <summary>
    /// The environment variable giving the install's public address, the core's own.
    /// </summary>
    public const string PublicUrlVariable = "FINTRACK_PUBLIC_URL";

    /// <summary>
    /// The link to <paramref name="path"/>, under the public address when it's set, the path alone when it isn't.
    /// </summary>
    /// <param name="path">The page's path, one of the tools' page paths.</param>
    public string For(string path)
    {
        string? publicUrl = configuration[PublicUrlVariable];
        if (string.IsNullOrWhiteSpace(publicUrl))
        {
            return path;
        }

        return $"{publicUrl.TrimEnd('/')}{path}";
    }
}
