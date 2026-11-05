using FinanceTracker.Design;

namespace FinanceTracker.Infrastructure.Exports.Pdf;

/// <summary>
/// The app's look for its PDFs. The light palette and money colours from <c>FinanceTracker.Design</c>, which the
/// client's theme and charts use too, and the PDFs' own sizes.
/// </summary>
internal static class PdfTheme
{
    public const string FontFamily = AppTypeface.FontFamily;

    public const string Primary = LightPalette.Primary;
    public const string TextPrimary = LightPalette.TextPrimary;
    public const string TextSecondary = LightPalette.TextSecondary;
    public const string Background = LightPalette.Background;
    public const string Divider = LightPalette.Divider;
    public const string TableLines = LightPalette.TableLines;
    public const string Warning = LightPalette.Warning;
    public const string Info = LightPalette.Info;

    public const string MoneyIn = MoneyColours.MoneyIn;
    public const string MoneyOut = MoneyColours.MoneyOut;

    /// <summary>
    /// The app's corners in points, 6 being the app's 8px at 72 points to 96 pixels an inch.
    /// </summary>
    public const float CornerRadius = AppShape.CornerRadiusPixels * 0.75f;

    public const float BodySize = 9;
    public const float SmallSize = 7.5f;
}
