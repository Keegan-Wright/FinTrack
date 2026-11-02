using FinanceTracker.Contracts.Calendar;
using CalendarEvent = FinanceTracker.Contracts.Calendar.CalendarEvent;
using DomainCalendarEvent = FinanceTracker.Domain.Calendar.CalendarEvent;
using DomainEventRecurrence = FinanceTracker.Domain.Calendar.EventRecurrence;

namespace FinanceTracker.Infrastructure.Calendar;

internal static class CalendarEventMapper
{
    /// <param name="goalName">Its savings goal's name, null without one.</param>
    /// <param name="today">Today where the user is, for when it's next on.</param>
    public static CalendarEvent ToContract(DomainCalendarEvent calendarEvent, string? goalName, DateOnly today) => new()
    {
        Id = calendarEvent.Id,
        Title = calendarEvent.Title,
        Date = calendarEvent.Date,
        Recurrence = ToContract(calendarEvent.Recurrence),
        Notes = calendarEvent.Notes,
        SavingsTarget = calendarEvent.SavingsTarget,
        SavingsGoalId = calendarEvent.SavingsGoalId,
        SavingsGoalName = goalName,
        RollsGoalOn = calendarEvent.RollsGoalOn,
        NextOn = calendarEvent.NextOn(today)
    };

    /// <param name="goalName">Its savings goal's name, null without one.</param>
    /// <param name="saved">What's saved in that goal, null without one.</param>
    public static CalendarEventItem ToItem(DomainCalendarEvent calendarEvent, string? goalName, decimal? saved) => new()
    {
        EventId = calendarEvent.Id,
        Title = calendarEvent.Title,
        Notes = calendarEvent.Notes,
        IsYearly = calendarEvent.Recurrence == DomainEventRecurrence.Yearly,
        SavingsTarget = calendarEvent.SavingsTarget,
        SavingsGoalName = goalName,
        Saved = saved
    };

    public static EventRecurrence ToContract(DomainEventRecurrence recurrence) => recurrence switch
    {
        DomainEventRecurrence.Yearly => EventRecurrence.Yearly,
        _ => EventRecurrence.None
    };

    public static DomainEventRecurrence ToDomain(EventRecurrence recurrence) => recurrence switch
    {
        EventRecurrence.Yearly => DomainEventRecurrence.Yearly,
        _ => DomainEventRecurrence.None
    };
}
