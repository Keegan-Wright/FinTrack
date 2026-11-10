using FinanceTracker.Domain.Notifications;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.Notifications.Email;
using FinanceTracker.Infrastructure.Notifications.Webhooks;
using FinanceTracker.Telemetry;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Notifications;

public class NotifierTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string Address = "sam@example.test";

    private static readonly DateTime Now = new(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc);

    private readonly INotificationPublisher _publisher = Substitute.For<INotificationPublisher>();
    private readonly IMailSender _mail = Substitute.For<IMailSender>();
    private readonly IWebhookSender _webhooks = Substitute.For<IWebhookSender>();

    [Before(Test)]
    public async Task SetUpAsync()
    {
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithEmail(Username, Address));
        _mail.IsAvailable.Returns(true);
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
            await _publisher.Received(1).NotificationAddedAsync(Scenario.UserMappings[Username]);
            await _mail.Received(1).SendAsync(
                Arg.Is<MailMessage>(x => x.To == Address && x.Subject == "Bank access has ended"),
                Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Notify_NewEvent_LogsItAndMarksTheSpanThatRaisedIt()
    {
        // Arrange
        CapturingLogger<Notifier> logger = new();
        using SpanRecorder spans = new();

        // Act
        using (spans.StartRoot())
        {
            await Notify("consent-ended:a", logger);
        }

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(logger.Entries.Single().Message)
                .StartsWith($"Notified user {Scenario.UserMappings[Username]} of ConsentEnded")
                .And.EndsWith("in app True, emailed True, webhook False");
            await Assert.That(spans.Root.Events.Select(x => x.Name)).IsEquivalentTo(["Notified"]);
            await Assert.That(spans.Root.GetTagItem(TelemetryTags.NotificationsRaised)).IsEqualTo(1);
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
            await _publisher.Received(1).NotificationAddedAsync(Arg.Any<Guid>());
            await _mail.Received(1).SendAsync(Arg.Any<MailMessage>(), Arg.Any<CancellationToken>());
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
            await _publisher.Received(1).NotificationAddedAsync(Arg.Any<Guid>());
            await _mail.DidNotReceive().SendAsync(Arg.Any<MailMessage>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Notify_NoMailServer_OnlyShowsInTheApp()
    {
        // Arrange
        _mail.IsAvailable.Returns(false);

        // Act
        bool raised = await Notify("consent-ended:a");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(raised).IsTrue();
            await _mail.DidNotReceive().SendAsync(Arg.Any<MailMessage>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Notify_EmailFails_StillShowsInTheApp()
    {
        // Arrange
        _mail.SendAsync(Arg.Any<MailMessage>(), Arg.Any<CancellationToken>())
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
        await _mail.Received(1).SendAsync(
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
            await _mail.DidNotReceive().SendAsync(Arg.Any<MailMessage>(), Arg.Any<CancellationToken>());
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
            await _webhooks.Received(1).SendAsync("https://hooks.example.test/fintrack", "secret",
                Arg.Is<WebhookPayload>(x => x.Kind == NotificationKind.ConsentEnded && x.Title == "Bank access has ended"),
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
            await _webhooks.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<WebhookPayload>(), Arg.Any<CancellationToken>());
            await _mail.Received(1).SendAsync(Arg.Any<MailMessage>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Notify_WebhookFails_RecordsWhyAndStillEmailsAndShowsIt()
    {
        // Arrange
        await GivenSettingsAsync(x => x.SetWebhook("https://hooks.example.test/fintrack", "secret"));
        _webhooks.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<WebhookPayload>(),
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
            await _mail.Received(1).SendAsync(Arg.Any<MailMessage>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Notify_NoWebhook_PostsNothing()
    {
        // Act
        await Notify("consent-ended:a");

        // Assert
        await _webhooks.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<WebhookPayload>(),
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
            await _publisher.DidNotReceive().NotificationAddedAsync(Arg.Any<Guid>());
            await _mail.Received(1).SendAsync(Arg.Any<MailMessage>(), Arg.Any<CancellationToken>());
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
            await _publisher.DidNotReceive().NotificationAddedAsync(Arg.Any<Guid>());
            await _mail.DidNotReceive().SendAsync(Arg.Any<MailMessage>(), Arg.Any<CancellationToken>());
            await _webhooks.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<WebhookPayload>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Notify_NotInTheAppAndNoMailServer_IsNotRaised()
    {
        // Arrange, emailed by their settings, but there's nothing to send it with
        _mail.IsAvailable.Returns(false);
        await GivenSettingsAsync(x => x.ShowInAppOnly([]));

        // Act
        bool raised = await Notify("consent-ended:a");

        // Assert
        await Assert.That(raised).IsFalse();
    }

    private async Task GivenSettingsAsync(Action<NotificationSettings> change)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        NotificationSettings settings = new(Scenario.UserMappings[Username]);
        change(settings);
        context.NotificationSettings.Add(settings);
        await context.SaveChangesAsync(CancellationToken);
    }

    private Task<bool> Notify(string key, ILogger<Notifier>? logger = null) =>
        new Notifier(CurrentUserFor(Username), DbContextFactory, logger ?? NullLogger<Notifier>.Instance, _publisher, _mail,
                TestEmails.Create(), _webhooks, PublicAddress.Unknown, new FixedTimeProvider(Now))
            .NotifyAsync(NotificationKind.ConsentEnded, key, "Bank access has ended",
                "FinTrack can no longer sync Bank.", "ProviderSetup", CancellationToken);
}
