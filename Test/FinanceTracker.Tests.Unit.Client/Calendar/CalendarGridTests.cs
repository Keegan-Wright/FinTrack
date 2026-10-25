using System.Globalization;
using FinanceTracker.Client.Components.Calendar;

namespace FinanceTracker.Tests.Unit.Client.Calendar;

public class CalendarGridTests
{
    [Test]
    public async Task WeekdayNames_InTheUk_StartOnMonday()
    {
        // Act
        IReadOnlyList<string> names = CalendarGrid.WeekdayNames(new CultureInfo("en-GB").DateTimeFormat);

        // Assert
        await Assert.That(names).IsEquivalentTo(["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"]);
    }

    [Test]
    public async Task WeekdayNames_InTheUs_StartOnSunday()
    {
        // Act
        IReadOnlyList<string> names = CalendarGrid.WeekdayNames(new CultureInfo("en-US").DateTimeFormat);

        // Assert
        await Assert.That(names[0]).IsEqualTo("Sun");
    }

    // October 2026 starts on a Thursday, March 2026 on a Sunday
    [Test]
    [Arguments(2026, 10, DayOfWeek.Monday, 3)]
    [Arguments(2026, 10, DayOfWeek.Sunday, 4)]
    [Arguments(2026, 3, DayOfWeek.Monday, 6)]
    [Arguments(2026, 3, DayOfWeek.Sunday, 0)]
    public async Task LeadingBlanks_FillUpToTheMonthsFirstDay(int year, int month, DayOfWeek firstDayOfWeek,
        int expected)
    {
        // Act
        int blanks = CalendarGrid.LeadingBlanks(new DateTime(year, month, 1), firstDayOfWeek);

        // Assert
        await Assert.That(blanks).IsEqualTo(expected);
    }
}
