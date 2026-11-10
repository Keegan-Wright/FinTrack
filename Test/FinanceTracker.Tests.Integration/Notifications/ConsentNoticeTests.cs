using FinanceTracker.Contracts.Automation;
using FinanceTracker.Contracts.Notifications;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Infrastructure.Automation;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;

namespace FinanceTracker.Tests.Integration.Notifications;

/// <summary>
/// The scheduled consent check, run now, telling a user their bank's access is about to end: under the bell and by
/// email to the mail catcher, linking to where they renew it. Not alongside the background sync tests either, which
/// run the same job, as only one run of it can go at once.
/// </summary>
[NotInParallel([JobNames.MonitorTokens, nameof(OpenBanking.BackgroundSyncTests)])]
public class ConsentNoticeTests : IntegrationTestBase
{
    [Test]
    public async Task ConsentEndingWithinAWeek_TellsThemInTheAppAndByEmail()
    {
        // Arrange, consent that ends in 3 days
        SignedInUser user = await RegisterUserAsync();
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("bank", user.Username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank"))
            .Do(s => s.BankConnectionScenario.WithConsentExpiresAt("bank", DateTime.UtcNow.AddDays(3)));
        SignedInUser admin = await RegisterAdminAsync();

        // Act
        await ApiFor(admin).Get<IAutomationService>().RunNowAsync(JobNames.MonitorTokens, CancellationToken);

        // Assert, under the bell
        Notification notification = await WaitForNotificationAsync(user);
        using (Assert.Multiple())
        {
            await Assert.That(notification.Kind).IsEqualTo(NotificationKind.ConsentEndingSoon);
            await Assert.That(notification.Title).EndsWith("access ends in 3 days");
            await Assert.That(notification.Link).IsEqualTo("ProviderSetup");
        }

        // Assert, and by email, linking back to the app
        IReadOnlyList<CaughtMail> mail = await App.Mail.WaitForAsync($"{user.Username}@fintrack.test",
            notification.Title, CancellationToken);
        CaughtMail email = mail.Single();
        using (Assert.Multiple())
        {
            await Assert.That(email.Subject).IsEqualTo(notification.Title);
            await Assert.That(email.Text).Contains(notification.Message);
            await Assert.That(email.Text).Contains("/ProviderSetup");
            await Assert.That(email.Html).Contains("Open in FinTrack");
            await Assert.That(email.Html).Contains("/ProviderSetup\"");
            await Assert.That(email.InlineContentIds).IsEquivalentTo(["fintrack-logo"]);
        }
    }

    [Test]
    public async Task CheckRunTwice_TellsThemOnce()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("bank", user.Username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank"))
            .Do(s => s.BankConnectionScenario.WithConsentExpiresAt("bank", DateTime.UtcNow.AddDays(3)));
        SignedInUser admin = await RegisterAdminAsync();
        IAutomationService automation = ApiFor(admin).Get<IAutomationService>();
        await automation.RunNowAsync(JobNames.MonitorTokens, CancellationToken);
        await WaitForNotificationAsync(user);

        // Act
        await RunAgainAsync(automation);

        // Assert
        await Assert.That(await ApiFor(user).Get<INotificationsService>().GetNotificationsAsync(50, CancellationToken)
            .CountAsync(CancellationToken)).IsEqualTo(1);
    }

    private async Task<Notification> WaitForNotificationAsync(SignedInUser user)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        timeout.CancelAfter(TestTimeouts.Job);
        INotificationsService notifications = ApiFor(user).Get<INotificationsService>();
        while (true)
        {
            List<Notification> raised = await notifications.GetNotificationsAsync(50, timeout.Token)
                .ToListAsync(timeout.Token);
            if (raised.Count > 0)
            {
                return raised.Single();
            }

            await Task.Delay(250, timeout.Token);
        }
    }

    // Once the first run has finished, it refuses to run while it's still going
    private async Task RunAgainAsync(IAutomationService automation)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        timeout.CancelAfter(TestTimeouts.Job);
        while (true)
        {
            try
            {
                await automation.RunNowAsync(JobNames.MonitorTokens, timeout.Token);
                break;
            }
            catch (JobAlreadyRunningException)
            {
                await Task.Delay(250, timeout.Token);
            }
        }

        // Long enough for the run to finish, nothing it raised would show otherwise
        await Task.Delay(TimeSpan.FromSeconds(3), timeout.Token);
    }
}
