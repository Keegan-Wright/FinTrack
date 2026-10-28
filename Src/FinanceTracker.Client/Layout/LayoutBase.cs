using System.ComponentModel;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceTracker.Client.Layout;

/// <summary>
/// Theme and dark mode handling for <see cref="MainLayout"/>. Dark mode follows the system setting
/// until toggled, and the layout re-renders whenever <see cref="ApplicationState"/> changes.
/// </summary>
public class LayoutBase : LayoutComponentBase
{
    internal readonly MudTheme _theme = AppTheme.Theme;

    /// <summary>
    /// The width the navigation drawer is docked open from, desktops. Tablets and phones start with it closed.
    /// </summary>
    internal const Breakpoint DrawerBreakpoint = Breakpoint.Lg;

    /// <summary>
    /// Closed until the first render knows the screen width.
    /// </summary>
    internal bool _drawerOpen;
    internal bool _isDarkMode = true;
    internal MudThemeProvider _mudThemeProvider = null!;

    /// <summary>
    /// The shared UI state, see <see cref="Client.ApplicationState"/>.
    /// </summary>
    [CascadingParameter]
    internal ApplicationState ApplicationState { get; set; } = null!;

    [Inject]
    private IBrowserViewportService BrowserViewportService { get; set; } = null!;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        ApplicationState.PropertyChanged += ApplicationStateOnPropertyChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _drawerOpen = await BrowserViewportService.IsBreakpointWithinWindowSizeAsync(Breakpoint.LgAndUp);
            _isDarkMode = await _mudThemeProvider.GetSystemDarkModeAsync();
            await _mudThemeProvider.WatchSystemDarkModeAsync(OnSystemPreferenceChanged);
            StateHasChanged();
        }
    }

    internal void ToggleDrawer() => _drawerOpen = !_drawerOpen;

    private Task OnSystemPreferenceChanged(bool newValue)
    {
        _isDarkMode = newValue;
        StateHasChanged();
        return Task.CompletedTask;
    }

    internal void ApplicationStateOnPropertyChanged(object? sender, PropertyChangedEventArgs e) => StateHasChanged();
}
