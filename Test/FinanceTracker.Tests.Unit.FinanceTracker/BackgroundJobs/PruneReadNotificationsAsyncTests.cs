using FinanceTracker.BackgroundJobs;
using FinanceTracker.Domain.Notifications;
using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Unit.FinanceTracker.BackgroundJobs;

public class PruneReadNotificationsAsyncTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";

    private static readonly DateTime Now = new(2026, 11, 9, 2, 30, 0, DateTimeKind.Utc);

    [Before(Test)]
    public async Task SetUpAsync() =>
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername));

    [Test]
    public async Task Execute_ReadMoreThan90DaysAgo_DeletesIt()
    {
        // Arrange
        await WithNotificationAsync("old", Username, readAt: Now.AddDays(-91));

        // Act
        await PruneAsync();

        // Assert
        await Assert.That(await RemainingAsync()).IsEmpty();
    }

    [Test]
    public async Task Execute_ReadWithin90Days_KeepsIt()
    {
        // Arrange
        await WithNotificationAsync("recent", Username, readAt: Now.AddDays(-89));

        // Act
        await PruneAsync();

        // Assert
        await Assert.That(await RemainingAsync()).IsEquivalentTo(["recent"]);
    }

    [Test]
    public async Task Execute_Unread_KeepsIt()
    {
        // Arrange
        await WithNotificationAsync("unread", Username, readAt: null);

        // Act
        await PruneAsync();

        // Assert
        await Assert.That(await RemainingAsync()).IsEquivalentTo(["unread"]);
    }

    [Test]
    public async Task Execute_SeveralUsers_PrunesEachAndKeepsTheRest()
    {
        // Arrange
        await WithNotificationAsync("mine-old", Username, readAt: Now.AddDays(-120));
        await WithNotificationAsync("mine-unread", Username, readAt: null);
        await WithNotificationAsync("theirs-old", OtherUsername, readAt: Now.AddDays(-95));
        await WithNotificationAsync("theirs-recent", OtherUsername, readAt: Now.AddDays(-3));

        // Act
        await PruneAsync();

        // Assert
        await Assert.That(await RemainingAsync()).IsEquivalentTo(["mine-unread", "theirs-recent"]);
    }

    private Task<TestingScenario> WithNotificationAsync(string key, string username, DateTime? readAt) =>
        Scenario.Do(s => s.NotificationScenario.WithNotification(key, username, userId =>
        {
            Notification notification = new(userId, NotificationKind.SyncFailed, key, "Bank couldn't sync",
                "FinTrack couldn't sync Bank.", null);
            if (readAt is not null)
            {
                notification.MarkRead(readAt.Value);
            }

            return notification;
        }));

    private async Task PruneAsync() =>
        await new PruneReadNotificationsAsync(DbContextFactory, new FixedTimeProvider(Now))
            .Execute(JobContexts.Scheduled(), CancellationToken);

    private async Task<List<string>> RemainingAsync()
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        return await context.Notifications.Select(static x => x.Key).ToListAsync(CancellationToken);
    }
}
