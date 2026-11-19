using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using Microsoft.AspNetCore.SignalR.Client;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="ISyncNotifications"/> over the server's sync hub. The first subscription starts the shared connection.
/// A connection that drops reconnects by itself, see <see cref="KeepTryingRetryPolicy"/>, and one that can't start,
/// e.g. while the server restarts, is tried again here on the same waits, so subscriptions are heard once it's back.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Client)]
[Singleton<ISyncNotifications>]
public sealed class HubSyncNotifications : ISyncNotifications, IDisposable
{
    private readonly HubConnection _connection;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _starting = new(1, 1);
    private readonly CancellationTokenSource _disposed = new();
    private readonly Lock _tryingLock = new();

    private Task? _tryingToStart;
    private bool _paused;

    /// <param name="connection">The sync hub's connection.</param>
    /// <param name="timeProvider">The clock.</param>
    public HubSyncNotifications(HubConnection connection, TimeProvider timeProvider)
    {
        _connection = connection;
        _timeProvider = timeProvider;

        _connection.Reconnecting += _ => PausedUntilReconnected(true);
        _connection.Reconnected += _ => PausedUntilReconnected(false);
        _connection.Closed += _ => OnClosed();
    }

    /// <inheritdoc/>
    public bool Paused => _paused;

    /// <inheritdoc/>
    public event EventHandler? PausedChanged;

    /// <inheritdoc/>
    public Task<IDisposable> SubscribeAsync(Func<Task> onSyncComplete, CancellationToken cancellationToken) =>
        StartedAsync(_connection.On(SyncHubContract.SyncComplete, onSyncComplete), cancellationToken);

    /// <inheritdoc/>
    public Task<IDisposable> SubscribeToSyncStartsAsync(Func<Task> onSyncStarted,
        CancellationToken cancellationToken) =>
        StartedAsync(_connection.On(SyncHubContract.SyncStarted, onSyncStarted), cancellationToken);

    /// <inheritdoc/>
    public Task<IDisposable> SubscribeToJobsAsync(Func<string, Task> onJobChanged,
        CancellationToken cancellationToken) =>
        StartedAsync(_connection.On(SyncHubContract.JobChanged, onJobChanged), cancellationToken);

    /// <inheritdoc/>
    public Task<IDisposable> SubscribeToNotificationsAsync(Func<Task> onNotificationAdded,
        CancellationToken cancellationToken) =>
        StartedAsync(_connection.On(SyncHubContract.NotificationAdded, onNotificationAdded), cancellationToken);

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposed.Cancel();
        _disposed.Dispose();
        _starting.Dispose();
    }

    // The subscription once the connection is up or being tried again, disposed only if the subscriber gave up
    /// <summary>
    /// Starts the hub for a new subscription. While the hub can't be reached, the subscription stands and hears once
    /// it's back.
    /// </summary>
    /// <param name="subscription">The subscription, disposed when the caller gives up.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task<IDisposable> StartedAsync(IDisposable subscription, CancellationToken cancellationToken)
    {
        try
        {
            await EnsureStartedAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            subscription.Dispose();
            throw;
        }
        catch (Exception)
        {
            // The hub can't be reached for now, the subscription stands and hears once it's back
            KeepTryingToStart();
        }

        return subscription;
    }

    /// <summary>
    /// Starts the hub unless it's already started, one subscriber at a time.
    /// </summary>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        // Subscribers can arrive together, only one of them may start the connection
        await _starting.WaitAsync(cancellationToken);
        try
        {
            if (_connection.State == HubConnectionState.Disconnected)
            {
                await _connection.StartAsync(cancellationToken);
            }
        }
        finally
        {
            _starting.Release();
        }
    }

    /// <summary>
    /// Given up on reconnecting, or closed by the server. Nothing to do once this has been disposed, the connection
    /// closes as it goes too.
    /// </summary>
    private Task OnClosed()
    {
        if (!_disposed.IsCancellationRequested)
        {
            KeepTryingToStart();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Pauses live updates and tries to start the connection again in the background, unless that's already
    /// happening.
    /// </summary>
    private void KeepTryingToStart()
    {
        SetPaused(true);
        lock (_tryingLock)
        {
            if (_tryingToStart is { IsCompleted: false })
            {
                return;
            }

            _tryingToStart = TryToStartUntilItDoesAsync(_disposed.Token);
        }
    }

    // The first try has failed by the time this runs, so it waits before each try
    /// <summary>
    /// Keeps trying to start the hub, waiting longer each time, until it starts.
    /// </summary>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task TryToStartUntilItDoesAsync(CancellationToken cancellationToken)
    {
        try
        {
            for (long failedTries = 1;; failedTries++)
            {
                await Task.Delay(KeepTryingRetryPolicy.WaitBefore(failedTries), _timeProvider, cancellationToken);
                if (await TryToStartAsync(cancellationToken))
                {
                    SetPaused(false);
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Disposed, the app is going
        }
    }

    /// <summary>
    /// Tries once to start the hub.
    /// </summary>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>Whether it started.</returns>
    private async Task<bool> TryToStartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await EnsureStartedAsync(cancellationToken);
            return true;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <summary>
    /// Notes the hub dropping or coming back, see <see cref="Paused"/>.
    /// </summary>
    /// <param name="paused">Whether it dropped.</param>
    private Task PausedUntilReconnected(bool paused)
    {
        SetPaused(paused);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sets <see cref="Paused"/>, raising <see cref="PausedChanged"/> when it changes.
    /// </summary>
    /// <param name="paused">Whether notifications are paused.</param>
    private void SetPaused(bool paused)
    {
        if (_paused == paused)
        {
            return;
        }

        _paused = paused;
        PausedChanged?.Invoke(this, EventArgs.Empty);
    }
}
