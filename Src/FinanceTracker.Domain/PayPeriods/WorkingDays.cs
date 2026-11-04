namespace FinanceTracker.Domain.PayPeriods;

/// <summary>
/// Weekdays that aren't bank holidays, see <see cref="BankHolidays"/>, the days pay can come in.
/// </summary>
public static class WorkingDays
{
    public static bool IsWorkingDay(DateOnly day) => !BankHolidays.IsWeekend(day) && !BankHolidays.IsBankHoliday(day);

    /// <summary>
    /// <paramref name="day"/> when it's a working day, otherwise the working day before it, as pay due on a weekend
    /// or bank holiday comes in early.
    /// </summary>
    public static DateOnly OnOrBefore(DateOnly day)
    {
        while (!IsWorkingDay(day))
        {
            day = day.AddDays(-1);
        }

        return day;
    }

    public static DateOnly LastIn(int year, int month) =>
        OnOrBefore(new DateOnly(year, month, DateTime.DaysInMonth(year, month)));
}
