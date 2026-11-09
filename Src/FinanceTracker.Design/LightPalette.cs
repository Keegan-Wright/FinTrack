namespace FinanceTracker.Design;

/// <summary>
/// The light mode colours, for the client's MudBlazor theme and charts and the server's PDFs. Each is named after the
/// MudBlazor palette property it sets.
/// </summary>
public static class LightPalette
{
    /// <summary>
    /// The brand green, for primary buttons, links and progress. Dark enough for 4.5:1 (WCAG AA) on its own 10% tint,
    /// as the current page's menu item is.
    /// </summary>
    public const string Primary = "#2A742E";

    /// <summary>The second accent, a blue.</summary>
    public const string Secondary = "#1976D2";

    /// <summary>The third accent, an amber.</summary>
    public const string Tertiary = "#FFB300";

    /// <summary>Behind the top bar.</summary>
    public const string AppbarBackground = "#FFFFFF";

    /// <summary>Text and icons on the top bar.</summary>
    public const string AppbarText = "#212121";

    /// <summary>Behind the page, and a PDF's tiles.</summary>
    public const string Background = "#F8F9FA";

    /// <summary>Cards, sections and dialogs.</summary>
    public const string Surface = "#FFFFFF";

    /// <summary>Behind the navigation drawer.</summary>
    public const string DrawerBackground = "#FFFFFF";

    /// <summary>Text in the navigation drawer.</summary>
    public const string DrawerText = "#424242";

    /// <summary>Icons in the navigation drawer.</summary>
    public const string DrawerIcon = "#616161";

    /// <summary>Body text.</summary>
    public const string TextPrimary = "#212121";

    /// <summary>
    /// Captions and other quieter text, and chart labels. Dark enough for 4.5:1 (WCAG AA) on every light surface,
    /// a chip's grey the lightest.
    /// </summary>
    public const string TextSecondary = "#696969";

    /// <summary>Icon buttons and other actions.</summary>
    public const string ActionDefault = "#546E7A";

    /// <summary>Actions that can't be used.</summary>
    public const string ActionDisabled = "#BDBDBD";

    /// <summary><see cref="ActionDefault"/> at the hover opacity, behind a hovered table row.</summary>
    public const string TableHover = "rgba(84,110,122,0.1)";

    /// <summary>Dividers between items and under headers.</summary>
    public const string Divider = "#E0E0E0";

    /// <summary>Lines between table rows, and the track of a progress bar.</summary>
    public const string TableLines = "#F5F5F5";

    /// <summary>Outlines, and a chart's grid lines.</summary>
    public const string LinesDefault = "#EEEEEE";

    /// <summary>
    /// Something that went well, e.g. a bill that's paid, the same green as money in. Dark enough for 4.5:1 (WCAG AA)
    /// as text on white, as an outlined chip's is, and for white text on it.
    /// </summary>
    public const string Success = MoneyColours.LightMoneyIn;

    /// <summary>
    /// <see cref="Success"/> darkened enough for 4.5:1 (WCAG AA) on its own 10% tint, as a text alert's message is.
    /// MudBlazor's own darkening isn't enough.
    /// </summary>
    public const string SuccessDarken = "#317534";

    /// <summary>
    /// Something that went wrong, the same red as money out. Dark enough for 4.5:1 (WCAG AA) as text on white and
    /// for white text on it.
    /// </summary>
    public const string Error = MoneyColours.LightMoneyOut;

    /// <summary>
    /// <see cref="Error"/> darkened enough for 4.5:1 (WCAG AA) on its own 10% tint, as a text alert's message is.
    /// MudBlazor's own darkening isn't enough.
    /// </summary>
    public const string ErrorDarken = "#BC2F2B";

    /// <summary>
    /// Something to look at, e.g. a budget off pace. A deep amber, dark enough for 4.5:1 (WCAG AA) as text on white
    /// and for white text on it, which no brighter orange is.
    /// </summary>
    public const string Warning = "#A65C00";

    /// <summary>
    /// <see cref="Warning"/> darkened enough for 4.5:1 (WCAG AA) on its own 10% tint, as a text alert's message is.
    /// MudBlazor's own darkening isn't enough.
    /// </summary>
    public const string WarningDarken = "#9C5700";

    /// <summary>
    /// Something to know, e.g. a debt's progress. Dark enough for 4.5:1 (WCAG AA) as text on white and for white
    /// text on it.
    /// </summary>
    public const string Info = MoneyColours.LightNeutral;

    /// <summary>
    /// A text alert's message colour, on <see cref="Info"/>'s own 10% tint. Info is dark enough for 4.5:1 (WCAG AA)
    /// there already, MudBlazor's own darkening would only make it darker.
    /// </summary>
    public const string InfoDarken = Info;

    /// <summary>
    /// The text on the light filled colour white is under 4.5:1 (WCAG AA) on: tertiary. The others keep MudBlazor's
    /// white.
    /// </summary>
    public const string DarkContrastText = TextPrimary;

    /// <summary>
    /// The text on every filled colour, For places MudBlazor
    /// doesn't style, e.g. an email's button.
    /// </summary>
    public const string LightContrastText = "#FFFFFF";
}
