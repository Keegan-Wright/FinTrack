using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using Microsoft.AspNetCore.SignalR.Client;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="ISyncNotifications"/> over the server's sync hub. The first subscription starts the shared connection,
/// which then reconnects by itself.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Client)]
[Singleton<ISyncNotifications>]
public sealed class HubSyncNotifications : ISyncNotifications, IDisposable
{
    private readonly HubConnection _connection;
    private readonly SemaphoreSlim _starting = new(1, 1);

    public HubSyncNotifications(HubConnection connection) => _connection = connection;

    public Task<IDisposable> SubscribeAsync(Func<Task> onSyncComplete, CancellationToken cancellationToken) =>
        StartedAsync(_connection.On(SyncHubContract.SyncComplete, onSyncComplete), cancellationToken);

    public Task<IDisposable> SubscribeToJobsAsync(Func<string, Task> onJobChanged,
        CancellationToken cancellationToken) =>
        StartedAsync(_connection.On(SyncHubContract.JobChanged, onJobChanged), cancellationToken);

    public void Dispose() => _starting.Dispose();

    // The subscription once the connection is up, or disposed if it can't start
    private async Task<IDisposable> StartedAsync(IDisposable subscription, CancellationToken cancellationToken)
    {
        try
        {
            await EnsureStartedAsync(cancellationToken);
        }
        catch
        {
            subscription.Dispose();
            throw;
        }

        return subscription;
    }

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
}
