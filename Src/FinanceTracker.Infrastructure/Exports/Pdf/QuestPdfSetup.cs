using QuestPDF;
using QuestPDF.Drawing;
using QuestPDF.Infrastructure;

namespace FinanceTracker.Infrastructure.Exports.Pdf;

/// <summary>
/// Sets QuestPDF up once and Inter the main font used over the app
/// from this assembly so nothing depends on the fonts the server has.
/// </summary>
internal static class QuestPdfSetup
{
    /// <summary>
    /// Whether it's set up, done the first time it's asked.
    /// </summary>
    private static readonly Lazy<bool> Done = new(Configure);

    /// <summary>
    /// Sets QuestPDF up, once.
    /// </summary>
    public static void EnsureConfigured() => _ = Done.Value;

    /// <summary>
    /// Sets the licence and registers the embedded fonts, without the server's own.
    /// </summary>
    /// <returns>True, once done.</returns>
    private static bool Configure()
    {
        Settings.License = LicenseType.Community;
        Settings.UseSystemFonts = false;

        foreach (string weight in (string[])["Regular", "SemiBold", "Bold"])
        {
            FontManager.RegisterFontFromEmbeddedResource(typeof(QuestPdfSetup).Assembly, $"Fonts.Inter-{weight}.ttf");
        }

        return true;
    }
}
