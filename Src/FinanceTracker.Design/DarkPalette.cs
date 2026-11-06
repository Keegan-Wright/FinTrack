namespace FinanceTracker.Design;

/// <summary>
/// The dark mode colours, for the client's MudBlazor theme and charts. Each is named after the MudBlazor palette
/// property it sets, see <see cref="LightPalette"/> for what each is for.
/// </summary>
public static class DarkPalette
{
    /// <summary>The brand green, see <see cref="LightPalette.Primary"/>.</summary>
    public const string Primary = "#4CAF50";

    /// <summary>The second accent, see <see cref="LightPalette.Secondary"/>.</summary>
    public const string Secondary = "#42A5F5";

    /// <summary>The third accent, see <see cref="LightPalette.Tertiary"/>.</summary>
    public const string Tertiary = "#FFCA28";

    /// <summary>Behind the top bar.</summary>
    public const string AppbarBackground = "#121212";

    /// <summary>Behind the page.</summary>
    public const string Background = "#0A0A0A";

    /// <summary>Cards, sections and dialogs.</summary>
    public const string Surface = "#161616";

    /// <summary>Behind the navigation drawer.</summary>
    public const string DrawerBackground = "#121212";

    /// <summary>Text in the navigation drawer.</summary>
    public const string DrawerText = "#E0E0E0";

    /// <summary>Icons in the navigation drawer.</summary>
    public const string DrawerIcon = "#B0B0B0";

    /// <summary>Body text.</summary>
    public const string TextPrimary = "#F5F5F5";

    /// <summary>Captions and other quieter text, and chart labels.</summary>
    public const string TextSecondary = "#B0B0B0";

    /// <summary>Icon buttons and other actions.</summary>
    public const string ActionDefault = "#90A4AE";

    /// <summary>Actions that can't be used.</summary>
    public const string ActionDisabled = "#424242";

    /// <summary>
    /// <see cref="ActionDefault"/> at the hover opacity, behind a hovered table row. MudBlazor's default darkens the
    /// row, which doesn't show on a dark surface.
    /// </summary>
    public const string TableHover = "rgba(144,164,174,0.1)";

    /// <summary>Dividers between items and under headers.</summary>
    public const string Divider = "#2A2A2A";

    /// <summary>Lines between table rows.</summary>
    public const string TableLines = "#1A1A1A";

    /// <summary>Outlines, and a chart's grid lines.</summary>
    public const string LinesDefault = "#2A2A2A";

    /// <summary>Something that went well, see <see cref="LightPalette.Success"/>.</summary>
    public const string Success = "#66BB6A";

    /// <summary>Something that went wrong, see <see cref="LightPalette.Error"/>.</summary>
    public const string Error = "#EF5350";

    /// <summary>Something to look at.</summary>
    public const string Warning = "#FFA726";

    /// <summary>Something to know.</summary>
    public const string Info = "#42A5F5";

    /// <summary>
    /// The text on every filled colour, e.g. a primary button, dark as white is under 4.5:1 (WCAG AA) on each of this
    /// palette's colours.
    /// </summary>
    public const string ContrastText = "#121212";
}
