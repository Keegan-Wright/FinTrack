namespace FinanceTracker.Contracts.Calendar;

/// <summary>
/// How often a calendar event comes round.
/// </summary>
public enum EventRecurrence
{
    /// <summary>
    /// Only on its date, e.g. a trip.
    /// </summary>
    None = 0,

    /// <summary>
    /// On its date every year, e.g. a birthday. 29 February falls on the 28th in other years.
    /// </summary>
    Yearly = 1
}
