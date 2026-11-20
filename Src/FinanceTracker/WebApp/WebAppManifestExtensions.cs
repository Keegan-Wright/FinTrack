using System.Text.Json;
using FinanceTracker.Design;

namespace FinanceTracker.WebApp;

/// <summary>
/// Serves the <see cref="WebAppManifest"/>, so FinTrack can be installed.
/// </summary>
public static class WebAppManifestExtensions
{
    /// <summary>
    /// Where the manifest is served, linked from every page.
    /// </summary>
    public const string ManifestPath = "/site.webmanifest";

    /// <summary>
    /// The manifest's content type.
    /// </summary>
    public const string ManifestContentType = "application/manifest+json";

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>
    /// The manifest. The app opens in dark mode, so its window and splash screen start dark too. The colours come
    /// from <see cref="DarkPalette"/> rather than being copied into a static file.
    /// </summary>
    internal static readonly WebAppManifest Manifest = new()
    {
        Id = "/",
        Name = "FinTrack",
        ShortName = "FinTrack",
        StartUrl = "/",
        Scope = "/",
        Display = "standalone",
        ThemeColor = DarkPalette.AppbarBackground,
        BackgroundColor = DarkPalette.Background,
        Icons =
        [
            new WebAppManifestIcon { Src = "/icon-192.png", Sizes = "192x192", Type = "image/png", Purpose = "any" },
            new WebAppManifestIcon { Src = "/icon-512.png", Sizes = "512x512", Type = "image/png", Purpose = "any" },
            new WebAppManifestIcon
            {
                Src = "/icon-maskable-512.png", Sizes = "512x512", Type = "image/png", Purpose = "maskable"
            }
        ]
    };

    extension(IEndpointRouteBuilder endpoints)
    {
        /// <summary>
        /// Serves the manifest at <see cref="ManifestPath"/> to anyone, as browsers fetch it without the user's
        /// cookies. Browsers may keep it for a day, it only changes with a new build.
        /// </summary>
        public IEndpointRouteBuilder MapWebAppManifest()
        {
            endpoints.MapGet(ManifestPath, static (HttpContext context) =>
                {
                    context.Response.Headers.CacheControl = "public, max-age=86400";
                    return TypedResults.Json(Manifest, ManifestJsonOptions, ManifestContentType);
                })
                .AllowAnonymous()
                .ExcludeFromDescription();
            return endpoints;
        }
    }
}
