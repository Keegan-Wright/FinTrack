using FinanceTracker.Client.Api;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.AspNetCore.SignalR.Client;

namespace FinanceTracker.Tests.Unit.Client.Api;

/// <summary>
/// The sync hub out of reach: subscribing still works and live updates say they're paused, while it keeps trying to
/// connect. Coming back is seen in the browser, see <c>SyncRefreshTests</c>.
/// </summary>
public sealed class HubSyncNotificationsTests : IAsyncDisposable
{
    private readonly RefusingHubHandler _hub = new();
    private readonly ManualTimeProvider _timeProvider = new();
    private readonly HubConnection _connection;
    private readonly HubSyncNotifications _notifications;

    public HubSyncNotificationsTests()
    {
        _connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/sync", options => options.HttpMessageHandlerFactory = _ => _hub)
            .WithAutomaticReconnect(new KeepTryingRetryPolicy())
            .Build();
        _notifications = new HubSyncNotifications(_connection, _timeProvider);
    }

    [Test]
    public async Task Subscribe_HubUnreachable_StillSubscribesAndSaysLiveUpdatesArePaused()
    {
        // Arrange
        int changes = 0;
        _notifications.PausedChanged += (_, _) => changes++;

        // Act
        using IDisposable subscription = await _notifications.SubscribeAsync(() => Task.CompletedTask,
            CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(subscription).IsNotNull();
            await Assert.That(_notifications.Paused).IsTrue();
            await Assert.That(changes).IsEqualTo(1);
        }
    }

    [Test]
    public async Task Subscribe_HubUnreachable_KeepsTryingEachTimeItsWaitIsUp()
    {
        // Arrange
        using IDisposable subscription = await _notifications.SubscribeAsync(() => Task.CompletedTask,
            CancellationToken.None);
        int triesAtFirst = _hub.Tries;

        // Act
        await WaitIsUpAsync(until: () => _hub.Tries > triesAtFirst);
        int triesAfterOneWait = _hub.Tries;
        await WaitIsUpAsync(until: () => _hub.Tries > triesAfterOneWait);

        // Assert, two more tries, one each wait, still paused
        using (Assert.Multiple())
        {
            await Assert.That(triesAtFirst).IsGreaterThan(0);
            await Assert.That(_hub.Tries).IsGreaterThan(triesAfterOneWait);
            await Assert.That(_notifications.Paused).IsTrue();
        }
    }

    [Test]
    public async Task Subscribe_SubscriberGivesUp_IsCancelled()
    {
        // Arrange
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        // Act, Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _notifications.SubscribeAsync(() => Task.CompletedTask, cancelled.Token));
    }

    public async ValueTask DisposeAsync()
    {
        _notifications.Dispose();
        await _connection.DisposeAsync();
    }

    /// <summary>
    /// Fires the wait before the next try, as soon as it's waiting, until <paramref name="until"/>.
    /// </summary>
    private async Task WaitIsUpAsync(Func<bool> until)
    {
        using CancellationTokenSource timeout = new(TestTimeouts.Wait);
        while (!until())
        {
            _timeProvider.Fire();
            await Task.Delay(10, timeout.Token);
        }
    }
}
