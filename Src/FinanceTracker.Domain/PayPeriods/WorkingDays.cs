namespace FinanceTracker.Domain.PayPeriods;

/// <summary>
/// Weekdays that aren't bank holidays, see <see cref="BankHolidays"/>, the days pay can come in.
/// </summary>
public static class WorkingDays
{
    /// <summary>
    /// Whether <paramref name="day"/> is a weekday that isn't a bank holiday.
    /// </summary>
    /// <param name="day">The day to check.</param>
    public static bool IsWorkingDay(DateOnly day) => !BankHolidays.IsWeekend(day) && !BankHolidays.IsBankHoliday(day);

    /// <summary>
    /// <paramref name="day"/> when it's a working day, otherwise the working day before it, as pay due on a weekend
    /// or bank holiday comes in early.
    /// </summary>
    /// <param name="day">The day pay is due.</param>
    public static DateOnly OnOrBefore(DateOnly day)
    {
        while (!IsWorkingDay(day))
        {
            day = day.AddDays(-1);
        }

        return day;
    }

    /// <summary>
    /// The last working day of <paramref name="month"/> in <paramref name="year"/>.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The month of the year, 1 to 12.</param>
    public static DateOnly LastIn(int year, int month) =>
        OnOrBefore(new DateOnly(year, month, DateTime.DaysInMonth(year, month)));
}
