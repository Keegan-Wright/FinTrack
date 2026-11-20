using FinanceTracker.Domain.Notifications;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure.Notifications.Email;
using FinanceTracker.Infrastructure.Notifications.Push;
using FinanceTracker.Infrastructure.Notifications.Webhooks;
using FinanceTracker.Telemetry;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Notifications;

public class NotifierTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string Address = "sam@example.test";

    private static readonly DateTime Now = new(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc);

    private readonly INotificationPublisher _notificationPublisher = Substitute.For<INotificationPublisher>();
    private readonly IMailSender _mailSender = Substitute.For<IMailSender>();
    private readonly IWebhookSender _webhookSender = Substitute.For<IWebhookSender>();
    private readonly IPushSender _pushSender = Substitute.For<IPushSender>();

    [Before(Test)]
    public async Task SetUpAsync()
    {
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithEmail(Username, Address));
        _mailSender.IsAvailable.Returns(true);
    }

    [Test]
    public async Task Notify_NewEvent_SavesItTellsTheirBrowsersAndEmailsIt()
    {
        // Act
        bool raised = await Notify("consent-ended:a");

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Notification saved = await context.Notifications.SingleAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(raised).IsTrue();
            await Assert.That(saved.UserId).IsEqualTo(Scenario.UserMappings[Username]);
            await Assert.That(saved.Title).IsEqualTo("Bank access has ended");
            await Assert.That(saved.IsRead).IsFalse();
            await _notificationPublisher.Received(1).NotificationAddedAsync(Scenario.UserMappings[Username]);
            await _mailSender.Received(1).SendAsync(
                Arg.Is<MailMessage>(x => x.To == Address && x.Subject == "Bank access has ended"),
                Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Notify_NewEvent_LogsItAndMarksTheSpanThatRaisedIt()
    {
        // Arrange
        CapturingLogger<Notifier> logger = new();
        using TestMetrics metrics = new();
        using MetricCollector<long> raised = metrics.Collect<long>(FinTrackMetrics.NotificationsRaisedName);
        using MetricCollector<long> delivered = metrics.Collect<long>(FinTrackMetrics.NotificationsDeliveredName);
        using SpanRecorder spans = new();

        // Act
        using (spans.StartRoot())
        {
            await Notify("consent-ended:a", logger, metrics.Metrics);
        }

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(logger.Entries.Single().Message)
                .StartsWith($"Notified user {Scenario.UserMappings[Username]} of ConsentEnded")
                .And.EndsWith("in app True, emailed True, webhook False, pushed False");
            await Assert.That(spans.Root.Events.Select(x => x.Name)).IsEquivalentTo(["Notified"]);
            await Assert.That(spans.Root.GetTagItem(TelemetryTags.NotificationsRaised)).IsEqualTo(1);
            await Assert.That(raised.LastMeasurement!.Tags[TelemetryTags.NotificationKind]).IsEqualTo("ConsentEnded");
            await Assert.That(delivered.LastMeasurement!.Tags[TelemetryTags.NotificationChannel]).IsEqualTo("email");
            await Assert.That(delivered.LastMeasurement.Tags[TelemetryTags.Outcome]).IsEqualTo("succeeded");
        }
    }

    [Test]
    public async Task Notify_SameEventAgain_RaisesItOnce()
    {
        // Arrange
        await Notify("consent-ended:a");

        // Act
        bool again = await Notify("consent-ended:a");

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(again).IsFalse();
            await Assert.That(await context.Notifications.CountAsync(CancellationToken)).IsEqualTo(1);
            await _notificationPublisher.Received(1).NotificationAddedAsync(Arg.Any<Guid>());
            await _mailSender.Received(1).SendAsync(Arg.Any<MailMessage>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Notify_KindTheyDontHaveEmailed_OnlyShowsInTheApp()
    {
        // Arrange
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            NotificationSettings settings = new(Scenario.UserMappings[Username]);
            settings.EmailOnly([NotificationKind.SyncFailed]);
            context.NotificationSettings.Add(settings);
            await context.SaveChangesAsync(CancellationToken);
        }

        // Act
        bool raised = await Notify("consent-ended:a");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(raised).IsTrue();
            await _notificationPublisher.Received(1).NotificationAddedAsync(Arg.Any<Guid>());
            await _mailSender.DidNotReceive().SendAsync(Arg.Any<MailMessage>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Notify_NoMailServer_OnlyShowsInTheApp()
    {
        // Arrange
        _mailSender.IsAvailable.Returns(false);

        // Act
        bool raised = await Notify("consent-ended:a");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(raised).IsTrue();
            await _mailSender.DidNotReceive().SendAsync(Arg.Any<MailMessage>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Notify_EmailFails_StillShowsInTheApp()
    {
        // Arrange
        _mailSender.SendAsync(Arg.Any<MailMessage>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("The mail server didn't answer"));

        // Act
        bool raised = await Notify("consent-ended:a");

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        NotificationSettings settings = await context.NotificationSettings.SingleAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(raised).IsTrue();
            await Assert.That(await context.Notifications.CountAsync(CancellationToken)).IsEqualTo(1);
            await Assert.That(settings.LastEmailAt).IsEqualTo(Now);
            await Assert.That(settings.LastEmailFailure).IsEqualTo("FinTrack couldn't reach the mail server.");
        }
    }

    [Test]
    public async Task Notify_Emailed_RecordsItWasSentWithoutChangingWhatTheyHaveEmailed()
    {
        // Act
        await Notify("consent-ended:a");

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        NotificationSettings settings = await context.NotificationSettings.SingleAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(settings.LastEmailAt).IsEqualTo(Now);
            await Assert.That(settings.LastEmailFailure).IsNull();
            await Assert.That(settings.Emailed).IsEquivalentTo(Enum.GetValues<NotificationKind>());
        }
    }

    [Test]
    public async Task Notify_Emailed_SendsTheBrandedEmail()
    {
        // Act
        await Notify("consent-ended:a");

        // Assert
        await _mailSender.Received(1).SendAsync(
            Arg.Is<MailMessage>(x => x.Images.Count == 1 && x.Html.Contains("FinTrack can no longer sync Bank.")),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Notify_EmailNotConfirmed_ShowsItInTheAppWithoutEmailingIt()
    {
        // Arrange, a mistyped address at registration doesn't get them
        await Scenario.Do(s => s.UserScenario.WithUnconfirmedEmail(Username));

        // Act
        await Notify("consent-ended:a");

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await _mailSender.DidNotReceive().SendAsync(Arg.Any<MailMessage>(), Arg.Any<CancellationToken>());
            await Assert.That(await context.Notifications.CountAsync(CancellationToken)).IsEqualTo(1);
        }
    }

    [Test]
    public async Task Notify_WebhookSet_PostsItSignedAndRecordsIt()
    {
        // Arrange
        await GivenSettingsAsync(x => x.SetWebhook("https://hooks.example.test/fintrack", "secret"));

        // Act
        await Notify("consent-ended:a");

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        NotificationSettings settings = await context.NotificationSettings.SingleAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await _webhookSender.Received(1).SendAsync("https://hooks.example.test/fintrack", "secret",
                Arg.Is<WebhookPayload>(x =>
                    x.Kind == NotificationKind.ConsentEnded && x.Title == "Bank access has ended"),
                Arg.Any<CancellationToken>());
            await Assert.That(settings.LastWebhookAt).IsEqualTo(Now);
            await Assert.That(settings.LastWebhookFailure).IsNull();
        }
    }

    [Test]
    public async Task Notify_KindNotPosted_OnlyEmailsIt()
    {
        // Arrange
        await GivenSettingsAsync(x =>
        {
            x.SetWebhook("https://hooks.example.test/fintrack", "secret");
            x.WebhookOnly([NotificationKind.SyncFailed]);
        });

        // Act
        await Notify("consent-ended:a");

        // Assert
        using (Assert.Multiple())
        {
            await _webhookSender.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<WebhookPayload>(), Arg.Any<CancellationToken>());
            await _mailSender.Received(1).SendAsync(Arg.Any<MailMessage>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Notify_WebhookFails_RecordsWhyAndStillEmailsAndShowsIt()
    {
        // Arrange
        await GivenSettingsAsync(x => x.SetWebhook("https://hooks.example.test/fintrack", "secret"));
        _webhookSender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<WebhookPayload>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new WebhookRejectedException(System.Net.HttpStatusCode.InternalServerError));

        // Act
        bool raised = await Notify("consent-ended:a");

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        NotificationSettings settings = await context.NotificationSettings.SingleAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(raised).IsTrue();
            await Assert.That(settings.LastWebhookFailure).IsEqualTo("The webhook answered 500, not success.");
            await Assert.That(settings.LastEmailFailure).IsNull();
            await _mailSender.Received(1).SendAsync(Arg.Any<MailMessage>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Notify_NoWebhook_PostsNothing()
    {
        // Act
        await Notify("consent-ended:a");

        // Assert
        await _webhookSender.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<WebhookPayload>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Notify_NotShownInTheApp_EmailsItWithoutTellingTheirBrowsers()
    {
        // Arrange
        await GivenSettingsAsync(x => x.ShowInAppOnly([NotificationKind.SyncFailed]));

        // Act
        bool raised = await Notify("consent-ended:a");

        // Assert, kept so it isn't raised again, but out of the app
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Notification saved = await context.Notifications.SingleAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(raised).IsTrue();
            await Assert.That(saved.InApp).IsFalse();
            await _notificationPublisher.DidNotReceive().NotificationAddedAsync(Arg.Any<Guid>());
            await _mailSender.Received(1).SendAsync(Arg.Any<MailMessage>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Notify_TurnedOffEverywhere_IsNotRaised()
    {
        // Arrange
        await GivenSettingsAsync(x =>
        {
            x.SetWebhook("https://hooks.example.test/fintrack", "secret");
            x.ShowInAppOnly([]);
            x.EmailOnly([]);
            x.WebhookOnly([]);
        });

        // Act
        bool raised = await Notify("consent-ended:a");

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(raised).IsFalse();
            await Assert.That(await context.Notifications.CountAsync(CancellationToken)).IsEqualTo(0);
            await _notificationPublisher.DidNotReceive().NotificationAddedAsync(Arg.Any<Guid>());
            await _mailSender.DidNotReceive().SendAsync(Arg.Any<MailMessage>(), Arg.Any<CancellationToken>());
            await _webhookSender.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<WebhookPayload>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Notify_NotInTheAppAndNoMailServer_IsNotRaised()
    {
        // Arrange, emailed by their settings, but there's nothing to send it with
        _mailSender.IsAvailable.Returns(false);
        await GivenSettingsAsync(x => x.ShowInAppOnly([]));

        // Act
        bool raised = await Notify("consent-ended:a");

        // Assert
        await Assert.That(raised).IsFalse();
    }

    [Test]
    public async Task Notify_WithADevice_PushesTheTitleAndMessage_AndRecordsIt()
    {
        // Arrange
        Guid deviceId = await GivenPushDeviceAsync();
        PushAnswers(PushOutcome.Delivered);

        // Act
        await Notify("consent-ended:a");

        // Assert, it says no amounts, so it's shown as it is
        PushDevice? device = await DeviceAsync(deviceId);
        using (Assert.Multiple())
        {
            await _pushSender.Received(1).SendAsync(Arg.Any<PushDevice>(),
                Arg.Is<PushMessage>(x => x.Title == "Bank access has ended"
                                         && x.Body == "FinTrack can no longer sync Bank."
                                         && x.Path == "ProviderSetup"),
                Arg.Any<CancellationToken>());
            await Assert.That(device!.LastPushAt).IsEqualTo(Now);
            await Assert.That(device.LastPushFailure).IsNull();
        }
    }

    [Test]
    public async Task NotifyWithAmounts_AsTheyStart_KeepsTheAmountsOffTheLockScreen()
    {
        // Arrange
        await GivenPushDeviceAsync();
        PushAnswers(PushOutcome.Delivered);

        // Act
        await NotifyBudgetNearAsync();

        // Assert, the notification itself keeps them
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Notification saved = await context.Notifications.SingleAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await _pushSender.Received(1).SendAsync(Arg.Any<PushDevice>(),
                Arg.Is<PushMessage>(x => x.Title == "Groceries is nearly used up"
                                         && x.Body == PushMessage.AmountsHiddenBody),
                Arg.Any<CancellationToken>());
            await Assert.That(saved.Title).IsEqualTo("Groceries has £12.50 left");
        }
    }

    [Test]
    public async Task NotifyWithAmounts_AmountsWantedOnTheLockScreen_PushesThem()
    {
        // Arrange
        await GivenSettingsAsync(static x => x.ChangeLockScreen(show: true));
        await GivenPushDeviceAsync();
        PushAnswers(PushOutcome.Delivered);

        // Act
        await NotifyBudgetNearAsync();

        // Assert
        await _pushSender.Received(1).SendAsync(Arg.Any<PushDevice>(),
            Arg.Is<PushMessage>(x => x.Title == "Groceries has £12.50 left" && x.Body == "£187.50 of £200.00 spent."),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Notify_KindNotPushed_DoesntPush()
    {
        // Arrange
        await GivenSettingsAsync(static x => x.PushOnly([NotificationKind.SyncFailed]));
        await GivenPushDeviceAsync();

        // Act
        await Notify("consent-ended:a");

        // Assert
        await _pushSender.DidNotReceive().SendAsync(Arg.Any<PushDevice>(), Arg.Any<PushMessage>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Notify_PushedOnly_IsStillRaised()
    {
        // Arrange, every other channel off
        _mailSender.IsAvailable.Returns(false);
        await GivenSettingsAsync(static x => x.ShowInAppOnly([]));
        await GivenPushDeviceAsync();
        PushAnswers(PushOutcome.Delivered);

        // Act
        bool raised = await Notify("consent-ended:a");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(raised).IsTrue();
            await _pushSender.Received(1).SendAsync(Arg.Any<PushDevice>(), Arg.Any<PushMessage>(),
                Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Notify_UserLocked_DoesntPush()
    {
        // Arrange
        await GivenPushDeviceAsync();
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            FinanceTrackerUser user = await context.Users.SingleAsync(
                x => x.Id == Scenario.UserMappings[Username], CancellationToken);
            user.LockoutEnd = FinanceTrackerUser.LockedByAdminUntil;
            await context.SaveChangesAsync(CancellationToken);
        }

        // Act
        await Notify("consent-ended:a");

        // Assert
        await _pushSender.DidNotReceive().SendAsync(Arg.Any<PushDevice>(), Arg.Any<PushMessage>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Notify_BrowserGone_RemovesTheDevice_AndCountsIt()
    {
        // Arrange
        Guid deviceId = await GivenPushDeviceAsync();
        PushAnswers(PushOutcome.Gone);
        using TestMetrics metrics = new();
        using MetricCollector<long> removed = metrics.Collect<long>(FinTrackMetrics.PushDevicesRemovedName);

        // Act
        await NotifyBudgetNearAsync(metrics.Metrics);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(await DeviceAsync(deviceId)).IsNull();
            await Assert.That(removed.GetMeasurementSnapshot().Sum(static x => x.Value)).IsEqualTo(1);
        }
    }

    [Test]
    public async Task Notify_PushFails_RecordsWhy_AndTheNotificationStands()
    {
        // Arrange
        Guid deviceId = await GivenPushDeviceAsync();
        _pushSender.SendAsync(Arg.Any<PushDevice>(), Arg.Any<PushMessage>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new PushRejectedException(System.Net.HttpStatusCode.TooManyRequests));

        // Act
        bool raised = await Notify("consent-ended:a");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(raised).IsTrue();
            await Assert.That((await DeviceAsync(deviceId))!.LastPushFailure)
                .IsEqualTo("The push service answered 429, not success.");
        }
    }

    private async Task GivenSettingsAsync(Action<NotificationSettings> change)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        NotificationSettings settings = new(Scenario.UserMappings[Username]);
        change(settings);
        context.NotificationSettings.Add(settings);
        await context.SaveChangesAsync(CancellationToken);
    }

    private Task<bool> Notify(string key, ILogger<Notifier>? logger = null, FinTrackMetrics? metrics = null) =>
        Subject(logger, metrics).NotifyAsync(NotificationKind.ConsentEnded, key, "Bank access has ended",
            "FinTrack can no longer sync Bank.", "ProviderSetup", CancellationToken);

    private Task<bool> NotifyBudgetNearAsync(FinTrackMetrics? metrics = null) =>
        Subject(metrics: metrics).NotifyWithAmountsAsync(NotificationKind.BudgetNearLimit, "budget-near:a",
            "Groceries has £12.50 left", "Groceries is nearly used up", "£187.50 of £200.00 spent.", "Budgets",
            CancellationToken);

    private Notifier Subject(ILogger<Notifier>? logger = null, FinTrackMetrics? metrics = null) =>
        new(CurrentUserFor(Username), DbContextFactory, logger ?? NullLogger<Notifier>.Instance,
            _notificationPublisher, _mailSender, TestEmails.Create(), _webhookSender, _pushSender,
            PublicAddress.Unknown, new FixedTimeProvider(Now), metrics ?? TestMetrics.Unobserved);

    private async Task<Guid> GivenPushDeviceAsync()
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        PushDevice device = new(Scenario.UserMappings[Username], $"https://push.example.test/{Guid.NewGuid():N}",
            "key", "secret", "Chrome on Android");
        context.PushDevices.Add(device);
        await context.SaveChangesAsync(CancellationToken);
        return device.Id;
    }

    private void PushAnswers(PushOutcome outcome) =>
        _pushSender.SendAsync(Arg.Any<PushDevice>(), Arg.Any<PushMessage>(), Arg.Any<CancellationToken>())
            .Returns(outcome);

    private async Task<PushDevice?> DeviceAsync(Guid id)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        return await context.PushDevices.SingleOrDefaultAsync(x => x.Id == id, CancellationToken);
    }
}
