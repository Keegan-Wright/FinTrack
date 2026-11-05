using FinanceTracker.Client.Extensions;
using FinanceTracker.Contracts.OpenBanking;
using FinanceTracker.Contracts.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace FinanceTracker.Client.Pages;

/// <summary>
/// Base for every page. A cancellation token tied to the page's lifetime, the shared loading overlay, a quiet
/// refresh once a background sync finishes, see <see cref="OnSyncedAsync"/>, and saving a file the user exports, see
/// <see cref="SaveFileAsync"/>.
/// </summary>
public class PageComponent : ComponentBase, IDisposable
{
    /// <summary>
    /// Cancelled when the user leaves the page, pass it to every service call so abandoned work stops.
    /// </summary>
    internal readonly CancellationTokenSource _cts = new();

    [CascadingParameter]
    private ApplicationState ApplicationState { get; set; } = null!;

    [Inject]
    private ISnackbar PageSnackbar { get; set; } = null!;

    [Inject]
    private IJSRuntime PageJS { get; set; } = null!;

    private bool _listeningForSyncs;
    private bool _refreshing;
    private bool _refreshAgain;
    private bool _disposed;

    /// <summary>
    /// Starts listening for syncs once the page has its parameters, the shared state among them.
    /// </summary>
    public override async Task SetParametersAsync(ParameterView parameters)
    {
        await base.SetParametersAsync(parameters);

        if (!_listeningForSyncs && !_disposed && ApplicationState is { } state)
        {
            _listeningForSyncs = true;
            state.Synced += RefreshAfterSyncAsync;
        }
    }

    /// <summary>
    /// Refreshes what the page shows once a background sync has finished, keeping the user's filters, range and
    /// place. Quietly, with no loading overlay, so the page changes in place. Pages showing synced data override it,
    /// the rest show nothing a sync changes. Never runs twice at once, a sync finishing meanwhile refreshes again
    /// after.
    /// </summary>
    protected virtual Task OnSyncedAsync() => Task.CompletedTask;

    private async Task RefreshAfterSyncAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (_refreshing)
        {
            _refreshAgain = true;
            return;
        }

        _refreshing = true;
        try
        {
            do
            {
                _refreshAgain = false;
                await InvokeAsync(RefreshOnceAsync);
            } while (_refreshAgain && !_disposed);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private async Task RefreshOnceAsync()
    {
        try
        {
            await OnSyncedAsync();
            StateHasChanged();
        }
        catch (OperationCanceledException) when (_disposed)
        {
            // The user left the page
        }
        catch (Exception)
        {
            // What's shown stays, just not the latest
            PageSnackbar.Add("Couldn't refresh after the sync, reload the page to see the latest",
                Severity.Warning);
        }
    }

    /// <summary>
    /// Has <paramref name="write"/> make the file, then saves it in the browser as <paramref name="fileName"/>, or
    /// says <paramref name="failure"/>. Nothing more once the user leaves the page.
    /// </summary>
    internal async Task SaveFileAsync(string fileName, string contentType, Func<Stream, CancellationToken, Task> write,
        string failure)
    {
        try
        {
            using MemoryStream file = new();
            await write(file, _cts.Token);
            file.Position = 0;
            await PageJS.SaveFileAsync(fileName, contentType, file, _cts.Token);
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            // The user left the page
        }
        catch (Exception)
        {
            PageSnackbar.Add(failure, Severity.Error);
        }
    }

    /// <summary>
    /// Cancels outstanding work, stops listening for syncs and clears the loading overlay, so it doesn't stay up on
    /// the next page.
    /// </summary>
    public void Dispose()
    {
        _disposed = true;
        if (_listeningForSyncs)
        {
            ApplicationState.Synced -= RefreshAfterSyncAsync;
        }

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
    /// Updates the navigation's count of transfers to review, after the user decides about one.
    /// </summary>
    internal async Task RefreshTransfersToReviewAsync(ITransfersService transfersService) =>
        ApplicationState.TransfersToReview = await transfersService.GetReviewCountAsync(_cts.Token);

    /// <summary>
    /// Updates the navigation's count of subscriptions to review, after the user confirms or dismisses one or adds a
    /// bill that covers one.
    /// </summary>
    /// <returns>The new count, for the page to show too.</returns>
    internal async Task<int> RefreshSubscriptionsToReviewAsync(IBillsService billsService)
    {
        int count = await billsService.GetSuggestionCountAsync(_cts.Token);
        ApplicationState.SubscriptionsToReview = count;
        return count;
    }

    /// <summary>
    /// Updates the count of connections to renew in the navigation and banner.
    /// </summary>
    internal void RefreshConnectionsToRenew(IEnumerable<BankConnectionSummary> connections) =>
        ApplicationState.CountConnectionsToRenew(connections);

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
