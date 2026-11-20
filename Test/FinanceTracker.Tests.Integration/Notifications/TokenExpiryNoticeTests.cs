using FinanceTracker.Contracts.Notifications;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Account.Tokens;
using FinanceTracker.Infrastructure.Automation;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared;

namespace FinanceTracker.Tests.Integration.Notifications;

/// <summary>
/// The scheduled API token check, run now, telling a user a token is about to expire. Under the bell and by email,
/// linking to where they make a new one. One run of the job at a time.
/// </summary>
[NotInParallel(JobNames.NotifyTokens)]
public class TokenExpiryNoticeTests : IntegrationTestBase
{
    [Test]
    public async Task TokenExpiringWithinAWeek_TellsThemInTheAppAndByEmail()
    {
        // Arrange, a token that expires in 3 days
        SignedInUser user = await RegisterUserAsync();
        IssuedToken issued = PersonalAccessTokenFormat.Issue();
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            context.PersonalAccessTokens.Add(new PersonalAccessToken(user.UserId, "Placeholder script",
                issued.LookupId, issued.SecretHash, DateTime.UtcNow.AddDays(3)));
            await context.SaveChangesAsync(CancellationToken);
        }

        SignedInUser admin = await RegisterAdminAsync();

        // Act
        await ApiFor(admin).Get<IAutomationService>().RunNowAsync(JobNames.NotifyTokens, CancellationToken);

        // Assert, under the bell and by email
        Notification notification = await WaitForNotificationAsync(user);
        IReadOnlyList<CaughtMail> mail = await App.Mail.WaitForAsync($"{user.Username}@fintrack.test",
            notification.Title, CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(notification.Kind).IsEqualTo(NotificationKind.ApiTokenExpiring);
            await Assert.That(notification.Title).IsEqualTo("API token Placeholder script expires in 3 days");
            await Assert.That(notification.Link).IsEqualTo("ApiTokens");
            await Assert.That(mail.Single().Text).Contains("/ApiTokens");
        }
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

            await Task.Delay(TimeSpan.FromMilliseconds(250), timeout.Token);
        }
    }
}
