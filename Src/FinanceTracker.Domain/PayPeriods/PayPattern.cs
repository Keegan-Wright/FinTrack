namespace FinanceTracker.Domain.PayPeriods;

/// <summary>
/// When pay comes in each month, worked out from the pays so far, see <see cref="Fitting"/>.
/// </summary>
/// <param name="Day">The day of the month for <see cref="PayDayRule.DayOfMonth"/>, 1 to 31, the month's last day in
/// shorter months. Unused for <see cref="PayDayRule.LastWorkingDay"/>.</param>
public sealed record PayPattern(PayDayRule Rule, int Day)
{
    public static PayPattern LastWorkingDay { get; } = new(PayDayRule.LastWorkingDay, 0);

    public static PayPattern OnDay(int day)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(day, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(day, 31);
        return new PayPattern(PayDayRule.DayOfMonth, day);
    }

    /// <summary>
    /// The day pay is due for <paramref name="month"/> of <paramref name="year"/>. Can be in the month before, e.g.
    /// pay due on Sunday the 1st comes in on Friday the 30th.
    /// </summary>
    public DateOnly ExpectedFor(int year, int month)
    {
        if (Rule == PayDayRule.LastWorkingDay)
        {
            return WorkingDays.LastIn(year, month);
        }

        int day = Math.Min(Day, DateTime.DaysInMonth(year, month));
        return WorkingDays.OnOrBefore(new DateOnly(year, month, day));
    }

    /// <summary>
    /// Whether pay due under this pattern lands on <paramref name="payDay"/>, for the month it's in or the next.
    /// </summary>
    public bool Fits(DateOnly payDay)
    {
        if (ExpectedFor(payDay.Year, payDay.Month) == payDay)
        {
            return true;
        }

        DateOnly next = payDay.AddMonths(1);
        return ExpectedFor(next.Year, next.Month) == payDay;
    }

    /// <summary>
    /// The pattern every one of <paramref name="payDays"/> fits, the last working day first, then the earliest day
    /// of the month. Null when none fits them all, e.g. one came in a few days early.
    /// </summary>
    public static PayPattern? Fitting(IReadOnlyCollection<DateOnly> payDays)
    {
        ArgumentNullException.ThrowIfNull(payDays);
        if (payDays.Count == 0)
        {
            return null;
        }

        if (payDays.All(LastWorkingDay.Fits))
        {
            return LastWorkingDay;
        }

        for (int day = 1; day <= 31; day++)
        {
            PayPattern pattern = OnDay(day);
            if (payDays.All(pattern.Fits))
            {
                return pattern;
            }
        }

        return null;
    }
}
