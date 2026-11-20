namespace FinanceTracker.WebApp;

/// <summary>
/// The web app manifest, which lets a browser install FinTrack to a home screen or desktop and open it in its own
/// window. Serialised with snake case names, as the manifest spec has them.
/// </summary>
public sealed class WebAppManifest
{
    /// <summary>
    /// Who the installed app is. Fixed, so moving <see cref="StartUrl"/> later doesn't make it a different app.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The app's name, on the install prompt and the splash screen.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The name under the home screen icon.
    /// </summary>
    public required string ShortName { get; init; }

    /// <summary>
    /// Where the installed app opens.
    /// </summary>
    public required string StartUrl { get; init; }

    /// <summary>
    /// The pages that stay in the app's window. Anything else opens in the browser.
    /// </summary>
    public required string Scope { get; init; }

    /// <summary>
    /// How the window looks, <c>standalone</c> for one without the browser's bars.
    /// </summary>
    public required string Display { get; init; }

    /// <summary>
    /// The colour of the window's title or status bar until the app sets its own.
    /// </summary>
    public required string ThemeColor { get; init; }

    /// <summary>
    /// Behind the splash screen while the app loads.
    /// </summary>
    public required string BackgroundColor { get; init; }

    /// <summary>
    /// The app's icons, in the sizes and shapes platforms ask for.
    /// </summary>
    public required IReadOnlyList<WebAppManifestIcon> Icons { get; init; }
}
