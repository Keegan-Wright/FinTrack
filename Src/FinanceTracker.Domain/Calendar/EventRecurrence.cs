namespace FinanceTracker.Domain.Calendar;

/// <summary>
/// How often a <see cref="CalendarEvent"/> comes round.
/// </summary>
public enum EventRecurrence
{
    /// <summary>
    /// Only on its date, e.g. a trip.
    /// </summary>
    None = 0,

    /// <summary>
    /// On its date every year from then, e.g. a birthday.
    /// </summary>
    Yearly = 1
}
