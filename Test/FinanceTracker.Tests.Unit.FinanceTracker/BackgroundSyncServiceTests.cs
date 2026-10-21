using FinanceTracker.Tests.Unit.Shared;

namespace FinanceTracker.Tests.Unit.FinanceTracker;

public class BackgroundSyncServiceTests : UnitTestBase
{
    [Test]
    public async Task NotifySyncComplete_IsReadByListener()
    {
        // Arrange
        var subject = new BackgroundSyncService();

        // Act
        await subject.NotifySyncComplete();

        // Assert
        await using var notifications = subject.GetSyncNotifications(CancellationToken).GetAsyncEnumerator(CancellationToken);
        await Assert.That(await notifications.MoveNextAsync()).IsTrue();
        await Assert.That(notifications.Current).IsTrue();
    }

    [Test]
    public async Task NotifySyncComplete_BeyondCapacity_KeepsMostRecentTen()
    {
        // Arrange
        var subject = new BackgroundSyncService();

        // Act
        for (var i = 0; i < 15; i++)
        {
            await subject.NotifySyncComplete();
        }

        // Assert
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(250));

        var received = 0;
        try
        {
            await foreach (var _ in subject.GetSyncNotifications(timeout.Token))
            {
                received++;
            }
        }
        catch (OperationCanceledException)
        {
            // The channel stays open, reading stops once it has been drained
        }

        await Assert.That(received).IsEqualTo(10);
    }

    [Test]
    public async Task GetSyncNotifications_WaitsForNotification()
    {
        // Arrange
        var subject = new BackgroundSyncService();
        await using var notifications = subject.GetSyncNotifications(CancellationToken).GetAsyncEnumerator(CancellationToken);

        // Act
        ValueTask<bool> next = notifications.MoveNextAsync();
        bool completedBeforeNotify = next.IsCompleted;
        await subject.NotifySyncComplete();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(completedBeforeNotify).IsFalse();
            await Assert.That(await next).IsTrue();
        }
    }
}
