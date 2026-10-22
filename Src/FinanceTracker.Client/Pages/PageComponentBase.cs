using Microsoft.AspNetCore.Components;

namespace FinanceTracker.Client.Pages;

/// <summary>
/// Base for every page. A cancellation token tied to the page's lifetime, and the shared loading overlay.
/// </summary>
public class PageComponent : ComponentBase, IDisposable
{
    /// <summary>
    /// Cancelled when the user leaves the page, pass it to every service call so abandoned work stops.
    /// </summary>
    internal readonly CancellationTokenSource _cts = new();

    [CascadingParameter]
    private ApplicationState ApplicationState { get; set; } = null!;

    /// <summary>
    /// Cancels outstanding work and clears the loading overlay, so it doesn't stay up on the next page.
    /// </summary>
    public void Dispose()
    {
        ApplicationState.Loading = false;
        ApplicationState.LoadingMessage = null;
        _cts.Cancel();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Shows or hides the loading overlay, with <paramref name="message"/> under the spinner.
    /// </summary>
    internal void SetLoadingState(bool loading, string? message = null)
    {
        ApplicationState.Loading = loading;
        ApplicationState.LoadingMessage = message;
        StateHasChanged();
    }
}
