using FinanceTracker.Domain.PayPeriods;

namespace FinanceTracker.Tests.Unit.Domain.PayPeriods;

[Category("Domain")]
public class PayPatternTests
{
    [Test]
    public async Task Fitting_PaysOnTheLastWorkingDay_IsTheLastWorkingDay()
    {
        // Arrange: 31 Aug is the summer bank holiday and 31 Oct a Saturday
        DateOnly[] pays = [new(2026, 8, 28), new(2026, 9, 30), new(2026, 10, 30)];

        // Act
        PayPattern? pattern = PayPattern.Fitting(pays);

        // Assert
        await Assert.That(pattern).IsEqualTo(PayPattern.LastWorkingDay);
    }

    [Test]
    public async Task Fitting_SameDayMovedEarlierForAWeekend_IsThatDay()
    {
        // Arrange: 15 Aug 2026 is a Saturday
        DateOnly[] pays = [new(2026, 7, 15), new(2026, 8, 14), new(2026, 9, 15)];

        // Act
        PayPattern? pattern = PayPattern.Fitting(pays);

        // Assert
        await Assert.That(pattern).IsEqualTo(PayPattern.OnDay(15));
    }

    [Test]
    public async Task Fitting_OneEarlyForNoReason_FitsNone()
    {
        // Arrange
        DateOnly[] pays = [new(2026, 7, 15), new(2026, 8, 12), new(2026, 9, 15)];

        // Act
        PayPattern? pattern = PayPattern.Fitting(pays);

        // Assert
        await Assert.That(pattern).IsNull();
    }

    [Test]
    [Arguments(2026, 11, 2026, 11, 30)]
    [Arguments(2027, 2, 2027, 2, 26)]
    public async Task ExpectedFor_LastWorkingDay(int year, int month, int expectedYear, int expectedMonth,
        int expectedDay)
    {
        // Act
        DateOnly due = PayPattern.LastWorkingDay.ExpectedFor(year, month);

        // Assert
        await Assert.That(due).IsEqualTo(new DateOnly(expectedYear, expectedMonth, expectedDay));
    }

    [Test]
    public async Task ExpectedFor_TheFirstOnASunday_IsTheFridayBefore()
    {
        // Act: 1 Nov 2026 is a Sunday
        DateOnly due = PayPattern.OnDay(1).ExpectedFor(2026, 11);

        // Assert
        await Assert.That(due).IsEqualTo(new DateOnly(2026, 10, 30));
    }

    [Test]
    public async Task ExpectedFor_DayBeyondAShortMonth_IsItsLastDay()
    {
        // Act: 28 Feb 2026 is a Saturday
        DateOnly due = PayPattern.OnDay(31).ExpectedFor(2026, 2);

        // Assert
        await Assert.That(due).IsEqualTo(new DateOnly(2026, 2, 27));
    }
}
