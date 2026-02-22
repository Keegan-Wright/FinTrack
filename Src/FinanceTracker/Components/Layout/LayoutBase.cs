using System.ComponentModel;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceTracker.Components.Layout;

public class LayoutBase : LayoutComponentBase
{
    internal readonly MudTheme _theme = new()
    {
        PaletteLight =
            new PaletteLight
            {
                Primary = "#2E7D32",
                Secondary = "#1976D2",
                Tertiary = "#FFB300",
                AppbarBackground = "#FFFFFF",
                AppbarText = "#212121",
                Background = "#F8F9FA",
                Surface = "#FFFFFF",
                DrawerBackground = "#FFFFFF",
                DrawerText = "#424242",
                DrawerIcon = "#616161",
                TextPrimary = "#212121",
                TextSecondary = "#757575",
                ActionDefault = "#546E7A",
                ActionDisabled = "#BDBDBD",
                Divider = "#E0E0E0",
                TableLines = "#F5F5F5",
                LinesDefault = "#EEEEEE",
                Success = "#43A047",
                Error = "#E53935",
                Warning = "#FB8C00",
                Info = "#1E88E5"
            },
        PaletteDark =
            new PaletteDark
            {
                Primary = "#4CAF50",
                Secondary = "#42A5F5",
                Tertiary = "#FFCA28",
                AppbarBackground = "#121212",
                Background = "#0A0A0A",
                Surface = "#161616",
                DrawerBackground = "#121212",
                DrawerText = "#E0E0E0",
                DrawerIcon = "#B0B0B0",
                TextPrimary = "#F5F5F5",
                TextSecondary = "#B0B0B0",
                ActionDefault = "#90A4AE",
                ActionDisabled = "#424242",
                Divider = "#212121",
                TableLines = "#1A1A1A",
                LinesDefault = "#212121",
                Success = "#66BB6A",
                Error = "#EF5350",
                Warning = "#FFA726",
                Info = "#42A5F5"
            },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "8px" },
        Typography = new Typography
        {
            Default =
                new DefaultTypography
                {
                    FontFamily = new[] { "Inter", "Roboto", "Helvetica", "Arial", "sans-serif" },
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

    internal bool _drawerOpen = true;
    internal bool _isDarkMode = true;
    internal MudThemeProvider _mudThemeProvider = null!;

    [CascadingParameter]
    internal ApplicationState ApplicationState { get; set; }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        ApplicationState.PropertyChanged += ApplicationStateOnPropertyChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _isDarkMode = await _mudThemeProvider.GetSystemDarkModeAsync();
            await _mudThemeProvider.WatchSystemDarkModeAsync(OnSystemPreferenceChanged);
            StateHasChanged();
        }
    }

    internal Task OnSystemPreferenceChanged(bool newValue)
    {
        _isDarkMode = newValue;
        StateHasChanged();
        return Task.CompletedTask;
    }

    internal void ApplicationStateOnPropertyChanged(object? sender, PropertyChangedEventArgs e) => StateHasChanged();
}
