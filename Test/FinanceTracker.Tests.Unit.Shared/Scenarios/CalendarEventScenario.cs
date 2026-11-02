using FinanceTracker.Domain.Calendar;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

public sealed class CalendarEventScenario : ScenarioBuilder
{
    public CalendarEventScenario(TestingScenario scenario) : base(scenario)
    {
    }

    /// <param name="create">Builds the event for the owning user id.</param>
    public Task<TestingScenario> WithEvent(string eventName, string username, Func<Guid, CalendarEvent> create)
    {
        Guid userId = Scenario.UserMappings[username];
        CalendarEvent calendarEvent = create(userId);

        Scenario.CalendarEventMappings.Add(eventName, calendarEvent.Id);
        return PersistAsync(context => context.CalendarEvents.Add(calendarEvent));
    }
}
