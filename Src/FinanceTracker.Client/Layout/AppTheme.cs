using FinanceTracker.Design;
using MudBlazor;

namespace FinanceTracker.Client.Layout;

/// <summary>
/// The app's palettes, shape and type from <see cref="LightPalette"/>, <see cref="DarkPalette"/>,
/// <see cref="AppShape"/> and <see cref="AppTypeface"/>, shared by <see cref="MainLayout"/> and the server's auth pages.
/// </summary>
public static class AppTheme
{
    public static MudTheme Theme { get; } = new()
    {
        PaletteLight =
            new PaletteLight
            {
                Primary = LightPalette.Primary,
                Secondary = LightPalette.Secondary,
                Tertiary = LightPalette.Tertiary,
                TertiaryContrastText = LightPalette.DarkContrastText,
                AppbarBackground = LightPalette.AppbarBackground,
                AppbarText = LightPalette.AppbarText,
                Background = LightPalette.Background,
                Surface = LightPalette.Surface,
                DrawerBackground = LightPalette.DrawerBackground,
                DrawerText = LightPalette.DrawerText,
                DrawerIcon = LightPalette.DrawerIcon,
                TextPrimary = LightPalette.TextPrimary,
                TextSecondary = LightPalette.TextSecondary,
                ActionDefault = LightPalette.ActionDefault,
                ActionDisabled = LightPalette.ActionDisabled,
                // Every *-hover colour is its colour at this opacity, MudBlazor's 6% barely shows
                HoverOpacity = 0.1,
                TableHover = LightPalette.TableHover,
                Divider = LightPalette.Divider,
                TableLines = LightPalette.TableLines,
                LinesDefault = LightPalette.LinesDefault,
                Success = LightPalette.Success,
                SuccessDarken = LightPalette.SuccessDarken,
                Error = LightPalette.Error,
                ErrorDarken = LightPalette.ErrorDarken,
                Warning = LightPalette.Warning,
                WarningDarken = LightPalette.WarningDarken,
                Info = LightPalette.Info,
                InfoDarken = LightPalette.InfoDarken
            },
        PaletteDark =
            new PaletteDark
            {
                Primary = DarkPalette.Primary,
                PrimaryContrastText = DarkPalette.ContrastText,
                Secondary = DarkPalette.Secondary,
                SecondaryContrastText = DarkPalette.ContrastText,
                Tertiary = DarkPalette.Tertiary,
                TertiaryContrastText = DarkPalette.ContrastText,
                AppbarBackground = DarkPalette.AppbarBackground,
                Background = DarkPalette.Background,
                Surface = DarkPalette.Surface,
                DrawerBackground = DarkPalette.DrawerBackground,
                DrawerText = DarkPalette.DrawerText,
                DrawerIcon = DarkPalette.DrawerIcon,
                TextPrimary = DarkPalette.TextPrimary,
                TextSecondary = DarkPalette.TextSecondary,
                ActionDefault = DarkPalette.ActionDefault,
                ActionDisabled = DarkPalette.ActionDisabled,
                HoverOpacity = 0.1,
                TableHover = DarkPalette.TableHover,
                Divider = DarkPalette.Divider,
                TableLines = DarkPalette.TableLines,
                LinesDefault = DarkPalette.LinesDefault,
                Success = DarkPalette.Success,
                SuccessContrastText = DarkPalette.ContrastText,
                Error = DarkPalette.Error,
                ErrorContrastText = DarkPalette.ContrastText,
                Warning = DarkPalette.Warning,
                WarningContrastText = DarkPalette.ContrastText,
                Info = DarkPalette.Info,
                InfoContrastText = DarkPalette.ContrastText
            },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = $"{AppShape.CornerRadiusPixels}px" },
        Typography = new Typography
        {
            Default =
                new DefaultTypography
                {
                    FontFamily = [.. AppTypeface.FontStack],
                    FontSize = ".875rem",
                    FontWeight = "400",
                    LineHeight = "1.43",
                    LetterSpacing = ".01071em"
                },
            H1 = new H1Typography { FontWeight = "700", FontSize = "3rem" },
            H2 = new H2Typography { FontWeight = "700", FontSize = "2.5rem" },
            H3 = new H3Typography { FontWeight = "700", FontSize = "2rem" },
            H4 = new H4Typography { FontWeight = "700", FontSize = "1.5rem" },
            H5 = new H5Typography { FontWeight = "700", FontSize = "1.25rem" },
            H6 = new H6Typography { FontWeight = "700", FontSize = "1rem" },
            Button = new ButtonTypography { FontWeight = "600", TextTransform = "none" },
            Subtitle1 = new Subtitle1Typography { FontWeight = "500" },
            Subtitle2 = new Subtitle2Typography { FontWeight = "500" }
        }
    };
}
