using FinanceTracker.Domain.Notifications;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.Notifications.Email;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Notifications;

public class NotifierTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string Address = "sam@example.test";

    private readonly INotificationPublisher _publisher = Substitute.For<INotificationPublisher>();
    private readonly IMailSender _mail = Substitute.For<IMailSender>();

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
        using (Assert.Multiple())
        {
            await Assert.That(raised).IsTrue();
            await Assert.That(await context.Notifications.CountAsync(CancellationToken)).IsEqualTo(1);
        }
    }

    private Task<bool> Notify(string key) =>
        new Notifier(CurrentUserFor(Username), DbContextFactory, NullLogger<Notifier>.Instance, _publisher, _mail,
                MailSettings.None)
            .NotifyAsync(NotificationKind.ConsentEnded, key, "Bank access has ended",
                "FinTrack can no longer sync Bank.", "ProviderSetup", CancellationToken);
}
