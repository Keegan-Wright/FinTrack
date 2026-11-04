using FinanceTracker.Domain.PayPeriods;

namespace FinanceTracker.Tests.Unit.Domain.PayPeriods;

[Category("Domain")]
public class BankHolidaysTests
{
    [Test]
    public async Task In_2026_HasEnglandAndWalesHolidays()
    {
        // Act
        IReadOnlySet<DateOnly> holidays = BankHolidays.In(2026);

        // Assert
        await Assert.That(holidays.Order()).IsEquivalentTo(
        [
            new DateOnly(2026, 1, 1), new DateOnly(2026, 4, 3), new DateOnly(2026, 4, 6), new DateOnly(2026, 5, 4),
            new DateOnly(2026, 5, 25), new DateOnly(2026, 8, 31), new DateOnly(2026, 12, 25),
            new DateOnly(2026, 12, 28)
        ], TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Arguments(2027, 12, 27, "Christmas on a Saturday moves to the Monday")]
    [Arguments(2027, 12, 28, "Boxing Day on a Sunday moves to the Tuesday")]
    [Arguments(2022, 12, 26, "Boxing Day on a Monday stays")]
    [Arguments(2022, 12, 27, "Christmas on a Sunday moves past Boxing Day")]
    [Arguments(2028, 1, 3, "New Year's Day on a Saturday moves to the Monday")]
    [Arguments(2027, 3, 26, "Good Friday")]
    public async Task IsBankHoliday_MovedAndMovingHolidays(int year, int month, int day, string because)
    {
        // Act
        bool holiday = BankHolidays.IsBankHoliday(new DateOnly(year, month, day));

        // Assert
        await Assert.That(holiday).IsTrue().Because(because);
    }

    [Test]
    [Arguments(2008, 3, 24, "Easter on 23 March, one of the earliest")]
    [Arguments(2019, 4, 22, "Easter on 21 April")]
    [Arguments(2024, 4, 1, "Easter on 31 March")]
    [Arguments(2025, 4, 21, "Easter on 20 April")]
    [Arguments(2038, 4, 26, "Easter on 25 April, the latest it can be")]
    [Arguments(2285, 3, 23, "Easter on 22 March, the earliest it can be")]
    [Arguments(2049, 4, 19, "Easter on 18 April, a week earlier for a late full moon")]
    public async Task In_EasterMonday(int year, int month, int day, string because)
    {
        // Act
        IReadOnlySet<DateOnly> holidays = BankHolidays.In(year);

        // Assert
        await Assert.That(holidays).Contains(new DateOnly(year, month, day)).Because(because);
    }

    [Test]
    [Arguments(2026, 9, 30, 2026, 9, 30)]
    [Arguments(2026, 8, 31, 2026, 8, 28)]
    [Arguments(2026, 10, 31, 2026, 10, 30)]
    [Arguments(2026, 12, 26, 2026, 12, 24)]
    public async Task OnOrBefore_SkipsWeekendsAndBankHolidays(int year, int month, int day, int expectedYear,
        int expectedMonth, int expectedDay)
    {
        // Act
        DateOnly working = WorkingDays.OnOrBefore(new DateOnly(year, month, day));

        // Assert
        await Assert.That(working).IsEqualTo(new DateOnly(expectedYear, expectedMonth, expectedDay));
    }
}
