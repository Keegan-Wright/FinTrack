using FinanceTracker.Tests.Unit.Shared;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceTracker.Tests.Unit.FinanceTracker;

public class SyncNotifierTests : UnitTestBase
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly SyncNotifier _subject = new(NullLogger<SyncNotifier>.Instance);

    [Test]
    public async Task NotifyAsync_CallsEverySubscriptionForTheUser()
    {
        // Arrange
        int firstTab = 0, secondTab = 0;
        using IDisposable first = _subject.Subscribe(_userId, () => Count(ref firstTab));
        using IDisposable second = _subject.Subscribe(_userId, () => Count(ref secondTab));

        // Act
        await _subject.NotifyAsync(_userId);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(firstTab).IsEqualTo(1);
            await Assert.That(secondTab).IsEqualTo(1);
        }
    }

    [Test]
    public async Task NotifyAsync_DoesNotCallOtherUsersSubscriptions()
    {
        // Arrange
        int otherUser = 0;
        using IDisposable subscription = _subject.Subscribe(Guid.NewGuid(), () => Count(ref otherUser));

        // Act
        await _subject.NotifyAsync(_userId);

        // Assert
        await Assert.That(otherUser).IsEqualTo(0);
    }

    [Test]
    public async Task NotifyAsync_WithNoSubscriptions_DoesNothing()
    {
        // Act & Assert
        await Assert.That(() => _subject.NotifyAsync(_userId)).ThrowsNothing();
    }

    [Test]
    public async Task NotifyAsync_DoesNotReplayToLaterSubscriptions()
    {
        // Arrange
        await _subject.NotifyAsync(_userId);
        int notified = 0;

        // Act
        using IDisposable subscription = _subject.Subscribe(_userId, () => Count(ref notified));

        // Assert
        await Assert.That(notified).IsEqualTo(0);
    }

    [Test]
    public async Task Dispose_StopsNotificationsToThatSubscriptionOnly()
    {
        // Arrange
        int closedTab = 0, openTab = 0;
        IDisposable closed = _subject.Subscribe(_userId, () => Count(ref closedTab));
        using IDisposable open = _subject.Subscribe(_userId, () => Count(ref openTab));

        // Act
        closed.Dispose();
        closed.Dispose();
        await _subject.NotifyAsync(_userId);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(closedTab).IsEqualTo(0);
            await Assert.That(openTab).IsEqualTo(1);
        }
    }

    [Test]
    public async Task NotifyAsync_WhenASubscriptionThrows_StillNotifiesTheRest()
    {
        // Arrange
        int healthyTab = 0;
        using IDisposable broken = _subject.Subscribe(_userId, () => throw new ObjectDisposedException("circuit"));
        using IDisposable healthy = _subject.Subscribe(_userId, () => Count(ref healthyTab));

        // Act
        await _subject.NotifyAsync(_userId);

        // Assert
        await Assert.That(healthyTab).IsEqualTo(1);
    }

    [Test]
    public async Task SubscribeAndDispose_WhenConcurrent_LeavesNoSubscriptionsBehind()
    {
        // Arrange
        int notified = 0;

        // Act
        await Parallel.ForAsync(0, 200, CancellationToken, (_, _) =>
        {
            IDisposable subscription = _subject.Subscribe(_userId, () => Count(ref notified));
            subscription.Dispose();
            return ValueTask.CompletedTask;
        });

        await _subject.NotifyAsync(_userId);

        // Assert
        await Assert.That(notified).IsEqualTo(0);
    }

    private static Task Count(ref int counter)
    {
        Interlocked.Increment(ref counter);
        return Task.CompletedTask;
    }
}
