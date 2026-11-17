using FinanceTracker.Domain.Calendar;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

/// <summary>
/// Seeds calendar events, mapped by name in <see cref="TestingScenario.CalendarEventMappings"/>.
/// </summary>
public sealed class CalendarEventScenario : ScenarioBuilder
{
    /// <param name="scenario">The scenario it seeds.</param>
    public CalendarEventScenario(TestingScenario scenario) : base(scenario)
    {
    }

    /// <summary>
    /// Adds a calendar event for the user.
    /// </summary>
    /// <param name="eventName">The name to map the event by.</param>
    /// <param name="username">The user who owns it, by their scenario name.</param>
    /// <param name="create">Builds the event for the owning user id.</param>
    public Task<TestingScenario> WithEvent(string eventName, string username, Func<Guid, CalendarEvent> create)
    {
        Guid userId = Scenario.UserMappings[username];
        CalendarEvent calendarEvent = create(userId);

        Scenario.CalendarEventMappings.Add(eventName, calendarEvent.Id);
        return PersistAsync(context => context.CalendarEvents.Add(calendarEvent));
    }
}
