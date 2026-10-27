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
    /// A page's initial data, loaded once. The server loads it while prerendering, and the browser picks up that copy
    /// from the page's <see cref="PersistentStateAttribute"/> property rather than fetching it again. Without a
    /// prerender (the browser navigating between pages) it loads here.
    /// </summary>
    /// <param name="persisted">The page's <see cref="PersistentStateAttribute"/> property, null until loaded.</param>
    /// <param name="loadingMessage">Shown under the loading overlay while loading.</param>
    /// <param name="load">Loads the data, with the page's cancellation token.</param>
    internal async Task<T> LoadOnceAsync<T>(T? persisted, string loadingMessage, Func<CancellationToken, Task<T>> load)
        where T : class
    {
        if (persisted is not null)
        {
            return persisted;
        }

        SetLoadingState(true, loadingMessage);
        try
        {
            return await load(_cts.Token);
        }
        finally
        {
            SetLoadingState(false);
        }
    }

    /// <summary>
    /// Shows the user's accounts as syncing in the layout, after queuing a sync. Cleared when the sync finishes.
    /// </summary>
    internal void ShowSyncing() => ApplicationState.Syncing = true;

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
