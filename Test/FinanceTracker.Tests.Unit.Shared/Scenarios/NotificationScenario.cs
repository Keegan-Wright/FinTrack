using FinanceTracker.Domain.Notifications;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

/// <summary>
/// Seeds notifications, mapped by name in <see cref="TestingScenario.NotificationMappings"/>.
/// </summary>
public sealed class NotificationScenario : ScenarioBuilder
{
    /// <param name="scenario">The scenario it seeds.</param>
    public NotificationScenario(TestingScenario scenario) : base(scenario)
    {
    }

    /// <summary>
    /// Adds a notification for the user.
    /// </summary>
    /// <param name="name">The name to map the notification by.</param>
    /// <param name="username">The user it's for, by their scenario name.</param>
    /// <param name="create">Builds the notification for the owning user id.</param>
    public Task<TestingScenario> WithNotification(string name, string username, Func<Guid, Notification> create)
    {
        Guid userId = Scenario.UserMappings[username];
        Notification notification = create(userId);

        Scenario.NotificationMappings.Add(name, notification.Id);
        return PersistAsync(context => context.Notifications.Add(notification));
    }
}
