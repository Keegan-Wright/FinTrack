using FinanceTracker.Client.Common.Extensions;
using FinanceTracker.Contracts.OpenBanking;
using FinanceTracker.Contracts.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace FinanceTracker.Client.Common.Pages;

/// <summary>
/// Base for every page. A cancellation token tied to the page's lifetime, the shared loading overlay, a quiet
/// refresh once a background sync finishes, see <see cref="OnSyncedAsync"/>, and saving a file the user exports, see
/// <see cref="SaveFileAsync"/>.
/// </summary>
public class PageComponent : ComponentBase, IDisposable
{
    /// <summary>
    /// Cancelled when the user leaves the page.
    /// </summary>
    private readonly CancellationTokenSource _pageCancellationSource = new();

    /// <summary>
    /// Cancelled when the user leaves the page, pass it to every service call so abandoned work stops.
    /// </summary>
    protected CancellationToken PageCancellation => _pageCancellationSource.Token;

    /// <summary>
    /// The shared UI state.
    /// </summary>
    [CascadingParameter]
    private ApplicationState ApplicationState { get; set; } = null!;

    /// <summary>
    /// Shows the page's messages.
    /// </summary>
    [Inject]
    private ISnackbar PageSnackbar { get; set; } = null!;

    /// <summary>
    /// Calls the app's scripts, to save files.
    /// </summary>
    [Inject]
    private IJSRuntime PageJS { get; set; } = null!;

    private bool _listeningForSyncs;
    private bool _refreshing;
    private bool _refreshAgain;
    private bool _disposed;

    /// <summary>
    /// Starts listening for syncs once the page has its parameters, the shared state among them.
    /// </summary>
    /// <param name="parameters">The parameters set.</param>
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

    /// <summary>
    /// Refreshes the page after a sync, again once more when another finishes meanwhile.
    /// </summary>
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

    /// <summary>
    /// Refreshes the page once. A failure leaves what's shown.
    /// </summary>
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
    /// <returns>Whether the file was saved.</returns>
    /// <param name="fileName">The file's name.</param>
    /// <param name="contentType">The file's media type.</param>
    /// <param name="write">Writes the file.</param>
    /// <param name="failure">What to tell the user when it fails.</param>
    protected async Task<bool> SaveFileAsync(string fileName, string contentType,
        Func<Stream, CancellationToken, Task> write, string failure)
    {
        try
        {
            using MemoryStream file = new();
            await write(file, PageCancellation);
            file.Position = 0;
            if (await PageJS.SaveFileAsync(fileName, contentType, file, PageCancellation))
            {
                return true;
            }

            PageSnackbar.Add(failure, Severity.Error);
        }
        catch (OperationCanceledException) when (PageCancellation.IsCancellationRequested)
        {
            // The user left the page
        }
        catch (Exception)
        {
            PageSnackbar.Add(failure, Severity.Error);
        }

        return false;
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
        // Cancelled, not disposed a load still running when the user moves on reads the token once it resumes, and a
        // disposed source throws there, which the layout shows as the next page failing. A cancelled load is ignored
        _pageCancellationSource.Cancel();
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
    protected async Task<T> LoadOnceAsync<T>(T? persisted, string loadingMessage, Func<CancellationToken, Task<T>> load)
        where T : class
    {
        if (persisted is not null)
        {
            return persisted;
        }

        SetLoadingState(true, loadingMessage);
        try
        {
            return await load(PageCancellation);
        }
        finally
        {
            SetLoadingState(false);
        }
    }

    /// <summary>
    /// Shows the user's accounts as syncing in the layout, after queuing a sync. Cleared when the sync finishes.
    /// </summary>
    protected void ShowSyncing() => ApplicationState.Syncing = true;

    /// <summary>
    /// Updates the navigation's count of transfers to review, after the user decides about one.
    /// </summary>
    /// <param name="transfersService">Gives the transfers to review.</param>
    protected async Task RefreshTransfersToReviewAsync(ITransfersService transfersService) =>
        ApplicationState.TransfersToReview = await transfersService.GetReviewCountAsync(PageCancellation);

    /// <summary>
    /// Updates the navigation's count of subscriptions to review, after the user confirms or dismisses one or adds a
    /// bill that covers one.
    /// </summary>
    /// <param name="billsService">Gives the subscriptions to review.</param>
    /// <returns>The new count, for the page to show too.</returns>
    protected async Task<int> RefreshSubscriptionsToReviewAsync(IBillsService billsService)
    {
        int count = await billsService.GetSuggestionCountAsync(PageCancellation);
        ApplicationState.SubscriptionsToReview = count;
        return count;
    }

    /// <summary>
    /// Updates the bell's and the navigation's count of unread notifications, after the user reads some.
    /// </summary>
    /// <param name="count">How many are unread.</param>
    protected void SetUnreadNotifications(int count) => ApplicationState.UnreadNotifications = count;

    /// <summary>
    /// Updates the count of connections to renew in the navigation and banner.
    /// </summary>
    /// <param name="connections">The user's bank connections.</param>
    protected void RefreshConnectionsToRenew(IEnumerable<BankConnectionSummary> connections) =>
        ApplicationState.CountConnectionsToRenew(connections);

    /// <summary>
    /// Shows or hides the loading overlay, with <paramref name="message"/> under the spinner.
    /// </summary>
    /// <param name="loading">Whether it's loading.</param>
    /// <param name="message">What's loading, null for the default.</param>
    protected void SetLoadingState(bool loading, string? message = null)
    {
        if (loading)
        {
            ApplicationState.ShowLoading(message);
        }
        else
        {
            ApplicationState.HideLoading();
        }

        StateHasChanged();
    }
}
