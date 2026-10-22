using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Services;
using Microsoft.AspNetCore.SignalR.Client;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="ISyncNotifications"/> over the server's sync hub. The first subscription starts the shared connection,
/// which then reconnects by itself.
/// </summary>
public sealed class HubSyncNotifications : ISyncNotifications, IDisposable
{
    private readonly HubConnection _connection;
    private readonly SemaphoreSlim _starting = new(1, 1);

    public HubSyncNotifications(HubConnection connection) => _connection = connection;

    public async Task<IDisposable> SubscribeAsync(Func<Task> onSyncComplete, CancellationToken cancellationToken)
    {
        IDisposable subscription = _connection.On(SyncHubContract.SyncComplete, onSyncComplete);

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

    public void Dispose() => _starting.Dispose();

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
