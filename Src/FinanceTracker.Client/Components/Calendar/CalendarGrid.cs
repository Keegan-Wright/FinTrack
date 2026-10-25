using System.Globalization;

namespace FinanceTracker.Client.Components.Calendar;

/// <summary>
/// The month grid's layout on the calendar page, the weekday headings and the blank cells before the first day, for a
/// week starting on the culture's first day.
/// </summary>
public static class CalendarGrid
{
    /// <summary>
    /// The abbreviated day names, starting from <paramref name="format"/>'s first day of the week.
    /// </summary>
    public static IReadOnlyList<string> WeekdayNames(DateTimeFormatInfo format) => Enumerable.Range(0, 7)
        .Select(i => format.AbbreviatedDayNames[((int)format.FirstDayOfWeek + i) % 7])
        .ToList();

    /// <summary>
    /// How many cells come before the month's first day in a week starting on <paramref name="firstDayOfWeek"/>.
    /// </summary>
    public static int LeadingBlanks(DateTime monthStart, DayOfWeek firstDayOfWeek) =>
        ((int)monthStart.DayOfWeek - (int)firstDayOfWeek + 7) % 7;
}
