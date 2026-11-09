using System.Globalization;
using FinanceTracker.Design;

namespace FinanceTracker.Infrastructure.Notifications.Email.Templates;

/// <summary>
/// The inline styles the email templates share, inline because email apps drop stylesheets. Colours, font and
/// corners from <c>FinanceTracker.Design</c>, as the app's.
/// </summary>
public static class EmailStyles
{
    /// <summary>
    /// The widest the email gets, narrower screens fit it to their width.
    /// </summary>
    public const int MaxWidthPixels = 560;

    public static string Font { get; } = $"font-family: {AppTypeface.CssFontStack};";

    public static string Corners { get; } =
        string.Create(CultureInfo.InvariantCulture, $"border-radius: {AppShape.CornerRadiusPixels}px;");

    public static string Page { get; } =
        $"margin: 0; padding: 0; width: 100%; background-color: {LightPalette.Background};";

    public static string Wordmark { get; } =
        $"{Font} font-size: 20px; font-weight: 700; line-height: 32px; color: {LightPalette.TextPrimary};";

    public static string Eyebrow { get; } =
        $"{Font} margin: 0 0 8px; font-size: 12px; font-weight: 700; letter-spacing: 0.06em; text-transform: uppercase;";

    public static string Heading { get; } =
        $"{Font} margin: 0 0 12px; font-size: 22px; font-weight: 700; line-height: 1.3; color: {LightPalette.TextPrimary};";

    public static string Body { get; } =
        $"{Font} margin: 0 0 12px; font-size: 16px; line-height: 1.5; color: {LightPalette.TextPrimary};";

    public static string Quiet { get; } =
        $"{Font} margin: 0; font-size: 13px; line-height: 1.5; color: {LightPalette.TextSecondary};";

    public static string Link { get; } = $"color: {LightPalette.Primary}; text-decoration: underline;";

    /// <summary>
    /// The card the content sits on, a band of <paramref name="accent"/> along its top.
    /// </summary>
    public static string Card(string accent) =>
        $"background-color: {LightPalette.Surface}; border: 1px solid {LightPalette.Divider}; " +
        $"border-top: 4px solid {accent}; {Corners} padding: 28px;";

    public static string Button { get; } =
        $"{Font} display: inline-block; padding: 12px 24px; font-size: 16px; font-weight: 600; line-height: 1.25; " +
        $"color: {LightPalette.LightContrastText}; background-color: {LightPalette.Primary}; text-decoration: none; {Corners}";
}
