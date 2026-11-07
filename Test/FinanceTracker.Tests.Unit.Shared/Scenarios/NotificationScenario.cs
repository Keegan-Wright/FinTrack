using FinanceTracker.Domain.Notifications;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

public sealed class NotificationScenario : ScenarioBuilder
{
    public NotificationScenario(TestingScenario scenario) : base(scenario)
    {
    }

    /// <param name="create">Builds the notification for the owning user id.</param>
    public Task<TestingScenario> WithNotification(string name, string username, Func<Guid, Notification> create)
    {
        Guid userId = Scenario.UserMappings[username];
        Notification notification = create(userId);

        Scenario.NotificationMappings.Add(name, notification.Id);
        return PersistAsync(context => context.Notifications.Add(notification));
    }
}
