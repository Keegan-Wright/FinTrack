namespace FinanceTracker.WebApp;

/// <summary>
/// One of the icons in the <see cref="WebAppManifest"/>.
/// </summary>
public sealed class WebAppManifestIcon
{
    /// <summary>
    /// Where the image is, from the site's root.
    /// </summary>
    public required string Src { get; init; }

    /// <summary>
    /// Its width and height, e.g. <c>512x512</c>.
    /// </summary>
    public required string Sizes { get; init; }

    /// <summary>
    /// The image's content type.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// <c>any</c> for an icon shown as it is, <c>maskable</c> for one with room around it for Android to crop to
    /// its own shape.
    /// </summary>
    public required string Purpose { get; init; }
}
