using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FinanceTracker.Client;

/// <summary>
/// UI state shared by the layout and the page inside it, cascaded from the root. Pages set it through
/// <see cref="Pages.PageComponent"/>, the layout listens for changes and shows the loading overlay.
/// </summary>
/// <remarks>
/// One instance per circuit (each browser tab).
/// </remarks>
public sealed class ApplicationState : INotifyPropertyChanged
{
    /// <summary>
    /// Whether the loading overlay covers the page.
    /// </summary>
    public bool Loading
    {
        get;
        set
        {
            if (value == field)
            {
                return;
            }

            field = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// The text under the loading overlay's spinner.
    /// </summary>
    public string? LoadingMessage
    {
        get;
        set
        {
            if (value == field)
            {
                return;
            }

            field = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Raised when <see cref="Loading"/> or <see cref="LoadingMessage"/> actually changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
