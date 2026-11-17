namespace FinanceTracker.Domain.PayPeriods;

/// <summary>
/// The days from one pay to the day before the next, named after the month it mostly covers, see
/// <see cref="PayCalendar"/>. A calendar month when there's no pay to go by.
/// </summary>
/// <param name="Start">The pay day.</param>
/// <param name="End">The day before the next pay, inclusive.</param>
/// <param name="Year">The year of the month it's named after.</param>
/// <param name="Month">The month it's named after, e.g. November for pay on 30 October.</param>
public readonly record struct PayPeriod(DateOnly Start, DateOnly End, int Year, int Month)
{
    /// <summary>
    /// Whether <paramref name="day"/> is in the period.
    /// </summary>
    /// <param name="day">The day to check.</param>
    public bool Contains(DateOnly day) => day >= Start && day <= End;

    /// <summary>
    /// How many days it has.
    /// </summary>
    public int Days => End.DayNumber - Start.DayNumber + 1;
}
