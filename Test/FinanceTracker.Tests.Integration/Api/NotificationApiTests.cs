using FinanceTracker.Contracts.Notifications;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using DomainKind = FinanceTracker.Domain.Notifications.NotificationKind;
using DomainNotification = FinanceTracker.Domain.Notifications.Notification;

namespace FinanceTracker.Tests.Integration.Api;

/// <summary>
/// The user's notifications and their email settings through the API, as the bell and the notifications page use it.
/// </summary>
public class NotificationApiTests : IntegrationTestBase
{
    [Test]
    public async Task Notifications_ListedCountedAndMarkedRead()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        await AddAsync(user, "first");
        await AddAsync(user, "second");
        INotificationsService service = ApiFor(user).Get<INotificationsService>();

        // Act
        List<Notification> listed = await service.GetNotificationsAsync(50, CancellationToken)
            .ToListAsync(CancellationToken);
        int unreadAtFirst = await service.GetUnreadCountAsync(CancellationToken);
        bool marked = await service.MarkReadAsync(listed[0].Id, CancellationToken);
        int unreadAfterOne = await service.GetUnreadCountAsync(CancellationToken);
        await service.MarkAllReadAsync(CancellationToken);
        int unreadAfterAll = await service.GetUnreadCountAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(listed.Select(static x => x.Title)).IsEquivalentTo(["second", "first"],
                TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(unreadAtFirst).IsEqualTo(2);
            await Assert.That(marked).IsTrue();
            await Assert.That(unreadAfterOne).IsEqualTo(1);
            await Assert.That(unreadAfterAll).IsEqualTo(0);
        }
    }

    [Test]
    public async Task MarkRead_SomeoneElses_IsNotFoundAndStaysUnread()
    {
        // Arrange
        SignedInUser owner = await RegisterUserAsync();
        SignedInUser other = await RegisterUserAsync();
        Guid theirs = await AddAsync(owner, "theirs");

        // Act
        bool marked = await ApiFor(other).Get<INotificationsService>().MarkReadAsync(theirs, CancellationToken);
        await ApiFor(other).Get<INotificationsService>().MarkAllReadAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(marked).IsFalse();
            await Assert.That(await ApiFor(owner).Get<INotificationsService>().GetUnreadCountAsync(CancellationToken))
                .IsEqualTo(1);
            await Assert.That(await ApiFor(other).Get<INotificationsService>()
                .GetNotificationsAsync(50, CancellationToken).CountAsync(CancellationToken)).IsEqualTo(0);
        }
    }

    [Test]
    public async Task Settings_ChangedThroughTheApi_AreKeptAndSayWhereEmailsGo()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        INotificationsService service = ApiFor(user).Get<INotificationsService>();

        // Act
        NotificationSettings atFirst = await service.GetSettingsAsync(CancellationToken);
        await service.ChangeSettingsAsync(new ChangeNotificationSettings { Emailed = [NotificationKind.SyncFailed] },
            CancellationToken);
        NotificationSettings after = await ApiFor(user).Get<INotificationsService>().GetSettingsAsync(CancellationToken);

        // Assert, the AppHost's mail catcher means email is available
        using (Assert.Multiple())
        {
            await Assert.That(atFirst.Emailed).IsEquivalentTo(Enum.GetValues<NotificationKind>());
            await Assert.That(atFirst.EmailAvailable).IsTrue();
            await Assert.That(atFirst.EmailAddress).IsEqualTo($"{user.Username}@fintrack.test");
            await Assert.That(after.Emailed).IsEquivalentTo([NotificationKind.SyncFailed]);
        }
    }

    private async Task<Guid> AddAsync(SignedInUser user, string title)
    {
        string name = UniqueName("notification");
        await Scenario.Do(s => s.NotificationScenario.WithNotification(name, user.Username, userId =>
            new DomainNotification(userId, DomainKind.SyncFailed, name, title, "A message.", null)));

        // Raised apart, so newest first has an order to keep
        await Task.Delay(20, CancellationToken);
        return Scenario.NotificationMappings[name];
    }
}
