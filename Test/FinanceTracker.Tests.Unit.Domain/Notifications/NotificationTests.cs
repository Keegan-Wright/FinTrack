using FinanceTracker.Domain.Notifications;

namespace FinanceTracker.Tests.Unit.Domain.Notifications;

[Category("Domain")]
public class NotificationTests
{
    private static readonly DateTime Now = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task Notification_Raised_IsUnreadWithWhatItSays()
    {
        // Act
        Notification notification = new(Guid.NewGuid(), NotificationKind.SyncFailed, "key", "  Title  ",
            " Message ", "ProviderSetup");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(notification.IsRead).IsFalse();
            await Assert.That(notification.Title).IsEqualTo("Title");
            await Assert.That(notification.Message).IsEqualTo("Message");
            await Assert.That(notification.Link).IsEqualTo("ProviderSetup");
        }
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("  ")]
    public async Task Notification_NoLink_IsNull(string? link)
    {
        // Act
        Notification notification = new(Guid.NewGuid(), NotificationKind.SyncFailed, "key", "Title", "Message", link);

        // Assert
        await Assert.That(notification.Link).IsNull();
    }

    [Test]
    [Arguments("", "Title", "Message")]
    [Arguments("key", "", "Message")]
    [Arguments("key", "Title", " ")]
    public async Task Notification_SomethingBlank_IsRefused(string key, string title, string message)
    {
        // Act, Assert
        await Assert.ThrowsAsync<ArgumentException>(() => Task.FromResult(
            new Notification(Guid.NewGuid(), NotificationKind.SyncFailed, key, title, message, null)));
    }

    [Test]
    public async Task MarkRead_Twice_KeepsWhenItWasFirstRead()
    {
        // Arrange
        Notification notification = new(Guid.NewGuid(), NotificationKind.SyncFailed, "key", "Title", "Message", null);

        // Act
        notification.MarkRead(Now);
        notification.MarkRead(Now.AddDays(1));

        // Assert
        await Assert.That(notification.ReadAt).IsEqualTo(Now);
    }
}
