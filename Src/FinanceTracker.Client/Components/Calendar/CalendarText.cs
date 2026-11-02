using FinanceTracker.Client.Components.Common;
using FinanceTracker.Contracts.Calendar;

namespace FinanceTracker.Client.Components.Calendar;

/// <summary>
/// How the user's events and debts' dates read in the calendar's day panel.
/// </summary>
public static class CalendarText
{
    /// <summary>
    /// What's saved for an event, e.g. <c>£40.00 of £100.00 saved in Birthday</c>, or what to save without a goal.
    /// Null when there's nothing to save.
    /// </summary>
    public static string? Saving(CalendarEventItem calendarEvent)
    {
        if (calendarEvent.SavingsGoalName is { } goalName && calendarEvent.Saved is { } saved)
        {
            if (calendarEvent.SavingsTarget is { } target)
            {
                return $"{CurrencyFormatter.Format(saved)} of {CurrencyFormatter.Format(target)} saved in {goalName}";
            }

            return $"{CurrencyFormatter.Format(saved)} saved in {goalName}";
        }

        if (calendarEvent.SavingsTarget is { } toSave)
        {
            return $"Save {CurrencyFormatter.Format(toSave)} for it";
        }

        return null;
    }

    /// <summary>
    /// e.g. <c>Every year</c> or <c>Event</c>.
    /// </summary>
    public static string Kind(CalendarEventItem calendarEvent) => calendarEvent.IsYearly ? "Every year" : "Event";

    /// <summary>
    /// What a debt's date is, e.g. <c>Forecast to be paid off</c> or <c>Your goal to pay it off, behind</c>.
    /// </summary>
    public static string DebtDate(CalendarDebtItem debt)
    {
        if (debt.Kind == CalendarDebtDate.PaidOff)
        {
            return "Forecast to be paid off";
        }

        if (debt.OnTrack == true)
        {
            return "Your goal to pay it off, on track";
        }

        return "Your goal to pay it off, behind";
    }

    /// <summary>
    /// e.g. <c>2 events</c>, for screen readers.
    /// </summary>
    public static string EventsLabel(int count) => count == 1 ? "1 event" : $"{count} events";
}
