using FinanceTracker.Contracts.Notifications;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.Notifications.Email;
using FinanceTracker.Infrastructure.Notifications.Webhooks;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using DomainKind = FinanceTracker.Domain.Notifications.NotificationKind;
using DomainNotification = FinanceTracker.Domain.Notifications.Notification;
using DomainSettings = FinanceTracker.Domain.Notifications.NotificationSettings;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Notifications;

public class NotificationsServiceTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";

    private static readonly DateTime Now = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);

    private readonly IMailSender _mail = Substitute.For<IMailSender>();
    private readonly IWebhookSender _webhooks = Substitute.For<IWebhookSender>();

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
    public async Task GetNotifications_OneNotShownInTheApp_LeavesItOutOfTheListAndCount()
    {
        // Arrange
        await AddAsync(Username, "shown");
        await AddAsync(Username, "emailed only", inApp: false);

        // Act
        List<Notification> notifications =
            await Subject(Username).GetNotificationsAsync(50, CancellationToken).ToListAsync(CancellationToken);
        int unread = await Subject(Username).GetUnreadCountAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(notifications.Select(static x => x.Title)).IsEquivalentTo(["shown"]);
            await Assert.That(unread).IsEqualTo(1);
        }
    }

    [Test]
    public async Task ChangeAlerts_ThenRead_KeepsThemForTheirOwnAccounts()
    {
        // Arrange
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("everyday", "bank"))
            .Do(s => s.BankAccountScenario.WithAccount("savings", "bank"))
            .Do(s => s.BankConnectionScenario.WithConnection("theirs", OtherUsername))
            .Do(s => s.BankAccountScenario.WithAccount("someone else's", "theirs"));
        Guid everyday = Scenario.AccountMappings["everyday"];
        Guid savings = Scenario.AccountMappings["savings"];

        // Act
        await Subject(Username).ChangeAlertsAsync(new ChangeAlertSettings
        {
            BudgetWarningPercent = 90,
            BillReminderDays = 5,
            LargeTransactionOver = 300m,
            LowBalance =
            [
                new LowBalanceThreshold { AccountId = everyday, Below = 100m },
                new LowBalanceThreshold { AccountId = Scenario.AccountMappings["someone else's"], Below = 50m }
            ]
        }, CancellationToken);
        NotificationSettings read = await Subject(Username).GetSettingsAsync(CancellationToken);

        // Assert, every account of theirs is listed, the one left out isn't watched
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        DomainSettings stored = await context.NotificationSettings.SingleAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(read.BudgetWarningPercent).IsEqualTo(90);
            await Assert.That(read.BillReminderDays).IsEqualTo(5);
            await Assert.That(read.LargeTransactionOver).IsEqualTo(300m);
            await Assert.That(read.LowBalance.Select(static x => x.AccountId)).IsEquivalentTo([everyday, savings]);
            await Assert.That(read.LowBalance.Single(x => x.AccountId == everyday).Below).IsEqualTo(100m);
            await Assert.That(read.LowBalance.Single(x => x.AccountId == savings).Below).IsNull();
            await Assert.That(stored.LowBalanceAlerts.Select(static x => x.AccountId)).IsEquivalentTo([everyday]);
        }
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
            InApp = [NotificationKind.SyncFailed], Emailed = [NotificationKind.SyncFailed], Webhooked = []
        }, CancellationToken);
        NotificationSettings changedAgain = await Subject(Username).ChangeSettingsAsync(
            new ChangeNotificationSettings
            {
                InApp = [NotificationKind.ConsentEnded],
                Emailed = [NotificationKind.ConsentEnded, NotificationKind.SyncFailed],
                Webhooked = [NotificationKind.ConsentEnded]
            },
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
            await Assert.That(read.Webhooked).IsEquivalentTo([NotificationKind.ConsentEnded]);
            await Assert.That(read.InApp).IsEquivalentTo([NotificationKind.ConsentEnded]);
        }
    }

    [Test]
    public async Task SetWebhook_PublicAddress_SetsItWithANewSecret()
    {
        // Act
        WebhookChange change = await Subject(Username).SetWebhookAsync(
            new SetWebhook { Url = " https://hooks.example.test/fintrack " }, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(change.Refusal).IsNull();
            await Assert.That(change.Settings!.WebhookUrl).IsEqualTo("https://hooks.example.test/fintrack");
            await Assert.That(change.Settings.WebhookSecret!.Length).IsEqualTo(43);
            await Assert.That(change.Settings.Webhooked).IsEquivalentTo(Enum.GetValues<NotificationKind>());
        }
    }

    [Test]
    public async Task SetWebhook_ChangedAddress_KeepsTheSecret()
    {
        // Arrange
        WebhookChange first = await Subject(Username).SetWebhookAsync(
            new SetWebhook { Url = "https://hooks.example.test/one" }, CancellationToken);

        // Act
        WebhookChange second = await Subject(Username).SetWebhookAsync(
            new SetWebhook { Url = "https://hooks.example.test/two" }, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(second.Settings!.WebhookUrl).IsEqualTo("https://hooks.example.test/two");
            await Assert.That(second.Settings.WebhookSecret).IsEqualTo(first.Settings!.WebhookSecret);
        }
    }

    [Test]
    public async Task SetWebhook_PrivateAddressWhenRefused_SaysWhyAndKeepsNothing()
    {
        // Act
        WebhookChange change = await Subject(Username).SetWebhookAsync(
            new SetWebhook { Url = "http://192.168.1.10:8123/api/webhook/fintrack" }, CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(change.Settings).IsNull();
            await Assert.That(change.Refusal).IsEqualTo(WebhookAddresses.PrivateRefusal);
            await Assert.That(await context.NotificationSettings.CountAsync(CancellationToken)).IsEqualTo(0);
        }
    }

    [Test]
    public async Task SetWebhook_PrivateAddressWhenAllowed_SetsIt()
    {
        // Act
        WebhookChange change = await Subject(Username, new WebhookSettings { AllowPrivateAddresses = true }).SetWebhookAsync(
            new SetWebhook { Url = "http://192.168.1.10:8123/api/webhook/fintrack" }, CancellationToken);

        // Assert
        await Assert.That(change.Settings!.WebhookUrl).IsEqualTo("http://192.168.1.10:8123/api/webhook/fintrack");
    }

    [Test]
    public async Task ReplaceWebhookSecret_WithAWebhook_SignsWithANewOne()
    {
        // Arrange
        WebhookChange set = await Subject(Username).SetWebhookAsync(
            new SetWebhook { Url = "https://hooks.example.test/fintrack" }, CancellationToken);

        // Act
        NotificationSettings? replaced = await Subject(Username).ReplaceWebhookSecretAsync(CancellationToken);
        NotificationSettings? without = await Subject(OtherUsername).ReplaceWebhookSecretAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(replaced!.WebhookSecret).IsNotEqualTo(set.Settings!.WebhookSecret);
            await Assert.That(without).IsNull();
        }
    }

    [Test]
    public async Task RemoveWebhook_ThenRead_HasNone()
    {
        // Arrange
        await Subject(Username).SetWebhookAsync(new SetWebhook { Url = "https://hooks.example.test/fintrack" },
            CancellationToken);

        // Act
        await Subject(Username).RemoveWebhookAsync(CancellationToken);
        NotificationSettings read = await Subject(Username).GetSettingsAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(read.WebhookUrl).IsNull();
            await Assert.That(read.WebhookSecret).IsNull();
        }
    }

    [Test]
    public async Task SendTestWebhook_WithAWebhook_PostsATestAndRecordsHowItWent()
    {
        // Arrange
        WebhookChange set = await Subject(Username).SetWebhookAsync(
            new SetWebhook { Url = "https://hooks.example.test/fintrack" }, CancellationToken);
        _webhooks.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<WebhookPayload>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("No such host"));

        // Act
        NotificationSettings? settings = await Subject(Username).SendTestWebhookAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await _webhooks.Received(1).SendAsync("https://hooks.example.test/fintrack", set.Settings!.WebhookSecret!,
                Arg.Is<WebhookPayload>(x => x.Event == WebhookPayload.TestEvent), Arg.Any<CancellationToken>());
            await Assert.That(settings!.LastWebhook!.At).IsEqualTo(Now);
            await Assert.That(settings.LastWebhook.Failure).IsEqualTo("FinTrack couldn't reach the webhook.");
        }
    }

    [Test]
    public async Task SendTestWebhook_NoWebhook_PostsNothing()
    {
        // Act
        NotificationSettings? settings = await Subject(Username).SendTestWebhookAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(settings).IsNull();
            await _webhooks.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<WebhookPayload>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task SendTestEmail_MailServerSetUp_EmailsThemAndRecordsItWasSent()
    {
        // Arrange
        _mail.IsAvailable.Returns(true);

        // Act
        NotificationSettings? settings = await Subject(Username).SendTestEmailAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await _mail.Received(1).SendAsync(
                Arg.Is<MailMessage>(x => x.To == "sam@example.test" && x.Subject == NotificationEmails.TestSubject),
                Arg.Any<CancellationToken>());
            await Assert.That(settings!.LastEmail!.At).IsEqualTo(Now);
            await Assert.That(settings.LastEmail.Sent).IsTrue();
            await Assert.That((await Subject(Username).GetSettingsAsync(CancellationToken)).LastEmail!.Sent).IsTrue();
        }
    }

    [Test]
    public async Task SendTestEmail_EmailNotConfirmed_SendsNothingAndSaysItIsnt()
    {
        // Arrange
        _mail.IsAvailable.Returns(true);
        await Scenario.Do(s => s.UserScenario.WithUnconfirmedEmail(Username));

        // Act
        NotificationSettings? sent = await Subject(Username).SendTestEmailAsync(CancellationToken);
        NotificationSettings settings = await Subject(Username).GetSettingsAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(sent).IsNull();
            await _mail.DidNotReceive().SendAsync(Arg.Any<MailMessage>(), Arg.Any<CancellationToken>());
            await Assert.That(settings.EmailAddress).IsEqualTo("sam@example.test");
            await Assert.That(settings.EmailConfirmed).IsFalse();
        }
    }

    [Test]
    public async Task SendTestEmail_Fails_RecordsWhyInWords()
    {
        // Arrange
        _mail.IsAvailable.Returns(true);
        _mail.SendAsync(Arg.Any<MailMessage>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new MailKit.Security.AuthenticationException("535 bad credentials"));

        // Act
        NotificationSettings? settings = await Subject(Username).SendTestEmailAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(settings!.LastEmail!.Sent).IsFalse();
            await Assert.That(settings.LastEmail.Failure).IsEqualTo("The mail server didn't accept FinTrack's sign in.");
        }
    }

    [Test]
    public async Task SendTestEmail_NoMailServer_SendsNothing()
    {
        // Act
        NotificationSettings? settings = await Subject(Username).SendTestEmailAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(settings).IsNull();
            await _mail.DidNotReceive().SendAsync(Arg.Any<MailMessage>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task SendTestEmail_NoAddress_SendsNothing()
    {
        // Arrange
        _mail.IsAvailable.Returns(true);
        await Scenario.Do(s => s.UserScenario.WithEmail(OtherUsername, ""));

        // Act
        NotificationSettings? settings = await Subject(OtherUsername).SendTestEmailAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(settings).IsNull();
            await _mail.DidNotReceive().SendAsync(Arg.Any<MailMessage>(), Arg.Any<CancellationToken>());
        }
    }

    private NotificationsService Subject(string username, WebhookSettings? webhookSettings = null) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<NotificationsService>.Instance,
            new FixedTimeProvider(Now), _mail, TestEmails.Create(), _webhooks, webhookSettings ?? new WebhookSettings(),
            PublicAddress.Unknown);

    private async Task<Guid> AddAsync(string username, string title, bool inApp = true)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        DomainNotification notification = new(Scenario.UserMappings[username], DomainKind.SyncFailed,
            Guid.NewGuid().ToString(), title, "A message", null, inApp);
        context.Notifications.Add(notification);
        await context.SaveChangesAsync(CancellationToken);

        // Raised apart, so newest first has an order to keep
        await Task.Delay(5, CancellationToken);
        return notification.Id;
    }
}
