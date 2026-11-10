using System.Globalization;
using System.Text.Json;
using FinanceTracker.Contracts.Notifications;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Infrastructure.Notifications.Email;
using FinanceTracker.Infrastructure.Notifications.Webhooks;
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
        await service.ChangeSettingsAsync(new ChangeNotificationSettings
        {
            InApp = Enum.GetValues<NotificationKind>(), Emailed = [NotificationKind.SyncFailed], Webhooked = []
        }, CancellationToken);
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

    [Test]
    public async Task TestEmail_ThroughTheApi_ArrivesBrandedAndIsRecorded()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();

        // Act
        NotificationSettings? settings = await ApiFor(user).Get<INotificationsService>()
            .SendTestEmailAsync(CancellationToken);

        // Assert
        IReadOnlyList<CaughtMail> mail = await App.Mail.WaitForAsync($"{user.Username}@fintrack.test",
            NotificationEmails.TestSubject, CancellationToken);
        CaughtMail email = mail.Single();
        using (Assert.Multiple())
        {
            await Assert.That(settings!.LastEmail!.Sent).IsTrue();
            await Assert.That(email.Subject).IsEqualTo("FinTrack test email");
            await Assert.That(email.Html).Contains("Email works");
            await Assert.That(email.Html).Contains("src=\"cid:fintrack-logo\"");
            await Assert.That(email.InlineContentIds).IsEquivalentTo(["fintrack-logo"]);
        }
    }

    [Test]
    public async Task TestWebhook_ThroughTheApi_ArrivesSignedWithTheirSecret()
    {
        // Arrange
        using WebhookCatcher catcher = new();
        SignedInUser user = await RegisterUserAsync();
        INotificationsService service = ApiFor(user).Get<INotificationsService>();
        WebhookChange set = await service.SetWebhookAsync(new SetWebhook { Url = catcher.Url.ToString() },
            CancellationToken);

        // Act
        NotificationSettings? settings = await service.SendTestWebhookAsync(CancellationToken);

        // Assert, checked as a receiver would check it
        CaughtPost post = await catcher.NextAsync(CancellationToken);
        long timestamp = long.Parse(post.Headers[WebhookSender.TimestampHeader], CultureInfo.InvariantCulture);
        using JsonDocument body = JsonDocument.Parse(post.Body);
        using (Assert.Multiple())
        {
            await Assert.That(post.Headers[WebhookSender.SignatureHeader])
                .IsEqualTo(WebhookSignature.Sign(set.Settings!.WebhookSecret!, timestamp, post.Body));
            await Assert.That(body.RootElement.GetProperty("event").GetString()).IsEqualTo("test");
            await Assert.That(settings!.LastWebhook!.Sent).IsTrue();
        }
    }

    [Test]
    public async Task Alerts_ChangedThroughTheApi_AreKept()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        INotificationsService service = ApiFor(user).Get<INotificationsService>();

        // Act
        NotificationSettings atFirst = await service.GetSettingsAsync(CancellationToken);
        await service.ChangeAlertsAsync(new ChangeAlertSettings
        {
            BudgetWarningPercent = 90, BillReminderDays = 7, LargeTransactionOver = 250m
        }, CancellationToken);
        NotificationSettings after = await ApiFor(user).Get<INotificationsService>().GetSettingsAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(atFirst.BudgetWarningPercent).IsEqualTo(80);
            await Assert.That(atFirst.BillReminderDays).IsEqualTo(3);
            await Assert.That(atFirst.LargeTransactionOver).IsNull();
            await Assert.That(after.BudgetWarningPercent).IsEqualTo(90);
            await Assert.That(after.BillReminderDays).IsEqualTo(7);
            await Assert.That(after.LargeTransactionOver).IsEqualTo(250m);
        }
    }

    [Test]
    public async Task Alerts_NoDaysBeforeABill_IsRefused()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();

        // Act
        HttpRequestException? exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            ApiFor(user).Get<INotificationsService>().ChangeAlertsAsync(new ChangeAlertSettings
            {
                BudgetWarningPercent = 80, BillReminderDays = 0
            }, CancellationToken));

        // Assert
        await Assert.That(exception!.StatusCode).IsEqualTo(System.Net.HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task SetWebhook_NotAnAddress_IsRefused()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();

        // Act
        HttpRequestException? exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            ApiFor(user).Get<INotificationsService>().SetWebhookAsync(new SetWebhook { Url = "not an address" },
                CancellationToken));

        // Assert
        await Assert.That(exception!.StatusCode).IsEqualTo(System.Net.HttpStatusCode.BadRequest);
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
