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

    internal bool _drawerOpen = true;
    internal bool _isDarkMode = true;
    internal MudThemeProvider _mudThemeProvider = null!;

    /// <summary>
    /// The shared UI state, see <see cref="Client.ApplicationState"/>.
    /// </summary>
    [CascadingParameter]
    internal ApplicationState ApplicationState { get; set; } = null!;

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

    private Task OnSystemPreferenceChanged(bool newValue)
    {
        _isDarkMode = newValue;
        StateHasChanged();
        return Task.CompletedTask;
    }

    internal void ApplicationStateOnPropertyChanged(object? sender, PropertyChangedEventArgs e) => StateHasChanged();
}
