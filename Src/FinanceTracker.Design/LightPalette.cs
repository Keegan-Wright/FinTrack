namespace FinanceTracker.Design;

/// <summary>
/// The light mode colours, for the client's MudBlazor theme and charts and the server's PDFs. Each is named after the
/// MudBlazor palette property it sets.
/// </summary>
public static class LightPalette
{
    /// <summary>The brand green, for primary buttons, links and progress.</summary>
    public const string Primary = "#2E7D32";

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

    /// <summary>Captions and other quieter text, and chart labels.</summary>
    public const string TextSecondary = "#757575";

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
    /// Something that went well, e.g. a bill that's paid. Money in uses <see cref="MoneyColours.MoneyIn"/>.
    /// </summary>
    public const string Success = "#43A047";

    /// <summary>Something that went wrong. Money out uses <see cref="MoneyColours.MoneyOut"/>.</summary>
    public const string Error = "#E53935";

    /// <summary>Something to look at, e.g. a budget off pace.</summary>
    public const string Warning = "#FB8C00";

    /// <summary>Something to know, e.g. a debt's progress.</summary>
    public const string Info = "#1E88E5";
}
