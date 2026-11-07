using FinanceTracker.Contracts.Notifications;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.Notifications.Email;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using DomainKind = FinanceTracker.Domain.Notifications.NotificationKind;
using DomainNotification = FinanceTracker.Domain.Notifications.Notification;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Notifications;

public class NotificationsServiceTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";

    private static readonly DateTime Now = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);

    private readonly IMailSender _mail = Substitute.For<IMailSender>();

    [Before(Test)]
    public async Task SetUpAsync() =>
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.UserScenario.WithEmail(Username, "sam@example.test"));

    [Test]
    public async Task GetNotifications_OnlyTheirsNewestFirst()
    {
        // Arrange
        await AddAsync(Username, "first");
        await AddAsync(OtherUsername, "someone else's");
        await AddAsync(Username, "second");

        // Act
        List<Notification> notifications =
            await Subject(Username).GetNotificationsAsync(50, CancellationToken).ToListAsync(CancellationToken);

        // Assert
        await Assert.That(notifications.Select(static x => x.Title)).IsEquivalentTo(["second", "first"],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task MarkRead_TheirsThenSomeoneElses_ReadsTheirsAndRefusesTheOther()
    {
        // Arrange
        Guid theirs = await AddAsync(Username, "theirs");
        Guid other = await AddAsync(OtherUsername, "someone else's");
        NotificationsService subject = Subject(Username);

        // Act
        bool readTheirs = await subject.MarkReadAsync(theirs, CancellationToken);
        bool readOther = await subject.MarkReadAsync(other, CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(readTheirs).IsTrue();
            await Assert.That(readOther).IsFalse();
            await Assert.That((await context.Notifications.SingleAsync(x => x.Id == theirs, CancellationToken))
                .ReadAt).IsEqualTo(Now);
            await Assert.That((await context.Notifications.SingleAsync(x => x.Id == other, CancellationToken))
                .IsRead).IsFalse();
            await Assert.That(await subject.GetUnreadCountAsync(CancellationToken)).IsEqualTo(0);
        }
    }

    [Test]
    public async Task MarkAllRead_ReadsOnlyTheirs()
    {
        // Arrange
        await AddAsync(Username, "one");
        await AddAsync(Username, "two");
        await AddAsync(OtherUsername, "someone else's");

        // Act
        await Subject(Username).MarkAllReadAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(await Subject(Username).GetUnreadCountAsync(CancellationToken)).IsEqualTo(0);
            await Assert.That(await Subject(OtherUsername).GetUnreadCountAsync(CancellationToken)).IsEqualTo(1);
        }
    }

    [Test]
    public async Task GetSettings_NeverChanged_EmailsEveryKindToTheirAddress()
    {
        // Arrange
        _mail.IsAvailable.Returns(true);

        // Act
        NotificationSettings settings = await Subject(Username).GetSettingsAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(settings.Emailed).IsEquivalentTo(Enum.GetValues<NotificationKind>());
            await Assert.That(settings.EmailAddress).IsEqualTo("sam@example.test");
            await Assert.That(settings.EmailAvailable).IsTrue();
        }
    }

    [Test]
    public async Task ChangeSettings_ThenRead_KeepsWhatTheyChose()
    {
        // Act
        await Subject(Username).ChangeSettingsAsync(new ChangeNotificationSettings
        {
            Emailed = [NotificationKind.SyncFailed]
        }, CancellationToken);
        NotificationSettings changedAgain = await Subject(Username).ChangeSettingsAsync(
            new ChangeNotificationSettings { Emailed = [NotificationKind.ConsentEnded, NotificationKind.SyncFailed] },
            CancellationToken);
        NotificationSettings read = await Subject(Username).GetSettingsAsync(CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(changedAgain.Emailed).IsEquivalentTo(
                [NotificationKind.ConsentEnded, NotificationKind.SyncFailed]);
            await Assert.That(read.Emailed).IsEquivalentTo(
                [NotificationKind.ConsentEnded, NotificationKind.SyncFailed]);
            await Assert.That(await context.NotificationSettings.CountAsync(CancellationToken)).IsEqualTo(1);
            await Assert.That(read.EmailAvailable).IsFalse();
        }
    }

    private NotificationsService Subject(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<NotificationsService>.Instance,
            new FixedTimeProvider(Now), _mail);

    private async Task<Guid> AddAsync(string username, string title)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        DomainNotification notification = new(Scenario.UserMappings[username], DomainKind.SyncFailed,
            Guid.NewGuid().ToString(), title, "A message", null);
        context.Notifications.Add(notification);
        await context.SaveChangesAsync(CancellationToken);

        // Raised apart, so newest first has an order to keep
        await Task.Delay(5, CancellationToken);
        return notification.Id;
    }
}
