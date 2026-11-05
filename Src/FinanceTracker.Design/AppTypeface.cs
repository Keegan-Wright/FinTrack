namespace FinanceTracker.Design;

/// <summary>
/// The app's font, with the fallbacks a browser uses when it isn't loaded.
/// </summary>
public static class AppTypeface
{
    /// <summary>
    /// Inter, which the pages load and the PDFs embed.
    /// </summary>
    public const string FontFamily = "Inter";

    /// <summary>
    /// <see cref="FontFamily"/> then its fallbacks, in order.
    /// </summary>
    public static IReadOnlyList<string> FontStack { get; } = [FontFamily, "Roboto", "Helvetica", "Arial", "sans-serif"];

    /// <summary>
    /// <see cref="FontStack"/> as a CSS <c>font-family</c> value.
    /// </summary>
    public static string CssFontStack { get; } = string.Join(", ", FontStack);
}
