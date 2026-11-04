namespace FinanceTracker.Domain.PayPeriods;

/// <summary>
/// England and Wales bank holidays, worked out rather than looked up New Year's Day, Good Friday, Easter Monday, the
/// early May, spring and summer holidays, Christmas Day and Boxing Day, each moved to the next free weekday when it
/// falls on a weekend.
/// </summary>
/// <remarks>
/// One-off holidays (a coronation, a jubilee) and ones moved for the year aren't known. Pay periods start on the pay
/// that actually came in, so they only affect the guess at a pay still to come.
/// </remarks>
public static class BankHolidays
{
    public static bool IsBankHoliday(DateOnly day) => In(day.Year).Contains(day);

    /// <summary>
    /// The bank holidays in <paramref name="year"/>.
    /// </summary>
    public static IReadOnlySet<DateOnly> In(int year)
    {
        DateOnly easterSunday = EasterSunday(year);

        HashSet<DateOnly> holidays =
        [
            easterSunday.AddDays(-2),
            easterSunday.AddDays(1),
            First(DayOfWeek.Monday, year, 5),
            Last(DayOfWeek.Monday, year, 5),
            Last(DayOfWeek.Monday, year, 8)
        ];

        // Weekday ones first, so one on a weekend moves past them, e.g. Christmas on a Sunday is kept on the Tuesday
        // after Boxing Day
        DateOnly[] fixedDays = [new(year, 1, 1), new(year, 12, 25), new(year, 12, 26)];

        foreach (DateOnly day in fixedDays.Where(static x => !IsWeekend(x)))
        {
            holidays.Add(day);
        }

        foreach (DateOnly day in fixedDays.Where(IsWeekend))
        {
            DateOnly substitute = day.AddDays(1);
            while (IsWeekend(substitute) || holidays.Contains(substitute))
            {
                substitute = substitute.AddDays(1);
            }

            holidays.Add(substitute);
        }

        return holidays;
    }

    internal static bool IsWeekend(DateOnly day) => day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    /// <summary>
    /// Easter Sunday the first Sunday after the first full moon on or after 21 March, using the church's tables of
    /// the moon rather than the real one. This is the anonymous Gregorian algorithm (Meeus, Jones and Butcher), its
    /// steps named for what they work out.
    /// </summary>
    private static DateOnly EasterSunday(int year)
    {
        // The moon's phases repeat on the same dates every 19 years, so where the year falls in that cycle says when
        // the moon is full
        int yearInMoonCycle = year % 19;

        int century = year / 100;
        int yearInCentury = year % 100;

        // The Gregorian calendar skips the leap day in three centuries out of four, which moves the dates
        int skippedLeapDays = century - (century / 4);

        // The church's tables also move the full moon a day eight times every 2,500 years, so they keep up with the
        // real moon
        int moonCorrection = (century - ((century + 8) / 25) + 1) / 3;

        // Days from 21 March to the full moon Easter follows, 0 to 29
        int daysToFullMoon = ((19 * yearInMoonCycle) + skippedLeapDays - moonCorrection + 15) % 30;

        // Days from that full moon to the Sunday after it, 0 to 6, from which weekday the year starts on
        int leapYearsInCentury = yearInCentury / 4;
        int yearInLeapCycle = yearInCentury % 4;
        int centuryInLeapCycle = century % 4;
        int daysToSunday = (32 + (2 * centuryInLeapCycle) + (2 * leapYearsInCentury) - daysToFullMoon -
                            yearInLeapCycle) % 7;

        // The church's tables never put that full moon after 18 April, so in the rare years the count goes past it
        // Easter is a week earlier, e.g. 2049
        int weekEarlier = (yearInMoonCycle + (11 * daysToFullMoon) + (22 * daysToSunday)) / 451;

        // 22 March is the earliest Easter can be, a full moon on the 21st that's a Saturday
        DateOnly earliest = new(year, 3, 22);
        return earliest.AddDays(daysToFullMoon + daysToSunday - (7 * weekEarlier));
    }

    private static DateOnly First(DayOfWeek dayOfWeek, int year, int month)
    {
        DateOnly day = new(year, month, 1);
        while (day.DayOfWeek != dayOfWeek)
        {
            day = day.AddDays(1);
        }

        return day;
    }

    private static DateOnly Last(DayOfWeek dayOfWeek, int year, int month)
    {
        DateOnly day = new(year, month, DateTime.DaysInMonth(year, month));
        while (day.DayOfWeek != dayOfWeek)
        {
            day = day.AddDays(-1);
        }

        return day;
    }
}
