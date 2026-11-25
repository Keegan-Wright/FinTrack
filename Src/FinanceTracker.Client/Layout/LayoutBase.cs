using System.ComponentModel;
#if FINTRACK_AI
using FinanceTracker.Client.Ai;
#endif
using FinanceTracker.Client.Common;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceTracker.Client.Layout;

/// <summary>
/// Theme and dark mode handling for <see cref="MainLayout"/>. Dark mode follows the system setting
/// until toggled, and the layout re-renders whenever <see cref="ApplicationState"/> changes.
/// </summary>
public class LayoutBase : LayoutComponentBase
{
    /// <summary>
    /// The app's theme.
    /// </summary>
    internal readonly MudTheme _theme = AppTheme.Theme;

    /// <summary>
    /// The width the navigation drawer is docked open from, desktops. Tablets and phones start with it closed.
    /// </summary>
    internal const Breakpoint DrawerBreakpoint = Breakpoint.Lg;

    /// <summary>
    /// Closed until the first render knows the screen width.
    /// </summary>
    internal bool _drawerOpen;

    /// <summary>
    /// Whether the dark theme is on.
    /// </summary>
    internal bool _isDarkMode = true;
    /// <summary>
    /// The theme provider, which says the system's dark mode.
    /// </summary>
    internal MudThemeProvider _mudThemeProvider = null!;

    /// <summary>
    /// The shared UI state, see <see cref="Common.ApplicationState"/>.
    /// </summary>
    [CascadingParameter]
    internal ApplicationState ApplicationState { get; set; } = null!;

    /// <summary>
    /// Says the browser's width, for whether the drawer starts open.
    /// </summary>
    [Inject]
    private IBrowserViewportService BrowserViewportService { get; set; } = null!;

#if FINTRACK_AI
    /// <summary>
    /// Loads the AI module, for its app bar button.
    /// </summary>
    [Inject]
    private AiModuleLoader AiModuleLoader { get; set; } = null!;

    /// <summary>
    /// A built-in module's app bar button once its assembly has loaded, null before.
    /// </summary>
    internal Type? ModuleAppBarButton => AiModuleLoader.AppBarButton;
#else
    /// <summary>
    /// A built-in module's app bar button, never one, no modules are built in.
    /// </summary>
    internal static Type? ModuleAppBarButton => null;
#endif

    /// <inheritdoc/>
    protected override void OnInitialized()
    {
        base.OnInitialized();
        ApplicationState.PropertyChanged += ApplicationStateOnPropertyChanged;
    }

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _drawerOpen = await BrowserViewportService.IsBreakpointWithinWindowSizeAsync(Breakpoint.LgAndUp);
            _isDarkMode = await _mudThemeProvider.GetSystemDarkModeAsync();
            await _mudThemeProvider.WatchSystemDarkModeAsync(OnSystemPreferenceChanged);
            StateHasChanged();
#if FINTRACK_AI
            // After the layout's own setup, so the drawer and dark mode don't wait on the download
            await AiModuleLoader.LoadAsync();
            StateHasChanged();
#endif
        }
    }

    /// <summary>
    /// Opens or closes the navigation drawer.
    /// </summary>
    internal void ToggleDrawer() => _drawerOpen = !_drawerOpen;

    /// <summary>
    /// Follows the system turning dark mode on or off.
    /// </summary>
    /// <param name="newValue">Whether it's on.</param>
    private Task OnSystemPreferenceChanged(bool newValue)
    {
        _isDarkMode = newValue;
        StateHasChanged();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Re-renders when the shared state changes.
    /// </summary>
    /// <param name="sender">The shared state.</param>
    /// <param name="e">What changed.</param>
    internal void ApplicationStateOnPropertyChanged(object? sender, PropertyChangedEventArgs e) => StateHasChanged();
}
