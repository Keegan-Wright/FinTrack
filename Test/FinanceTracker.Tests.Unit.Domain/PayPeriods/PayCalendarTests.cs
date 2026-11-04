using FinanceTracker.Domain.PayPeriods;

namespace FinanceTracker.Tests.Unit.Domain.PayPeriods;

[Category("Domain")]
public class PayCalendarTests
{
    private static readonly DateOnly[] Pays = [new(2026, 8, 28), new(2026, 9, 30), new(2026, 10, 30)];

    [Test]
    public async Task Containing_Today_IsFromTheLastPayToTheDayBeforeTheNext()
    {
        // Arrange
        PayCalendar calendar = new(PayPattern.LastWorkingDay, Pays, new DateOnly(2026, 11, 4));

        // Act
        PayPeriod period = calendar.Containing(new DateOnly(2026, 11, 4));

        // Assert
        await Assert.That(period).IsEqualTo(new PayPeriod(new DateOnly(2026, 10, 30), new DateOnly(2026, 11, 29),
            2026, 11));
    }

    [Test]
    public async Task Named_ByTheMonthItMostlyCovers()
    {
        // Arrange
        PayCalendar calendar = new(PayPattern.LastWorkingDay, Pays, new DateOnly(2026, 11, 4));

        // Act
        PayPeriod september = calendar.Named(2026, 9);
        PayPeriod october = calendar.Named(2026, 10);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(september).IsEqualTo(new PayPeriod(new DateOnly(2026, 8, 28),
                new DateOnly(2026, 9, 29), 2026, 9));
            await Assert.That(october).IsEqualTo(new PayPeriod(new DateOnly(2026, 9, 30),
                new DateOnly(2026, 10, 29), 2026, 10));
        }
    }

    [Test]
    public async Task Containing_PayCameInEarly_StartsOnTheDayItCame()
    {
        // Arrange: due on 30 Sept, came on the 25th
        DateOnly[] pays = [new(2026, 8, 28), new(2026, 9, 25), new(2026, 10, 30)];
        PayCalendar calendar = new(PayPattern.LastWorkingDay, pays, new DateOnly(2026, 11, 4));

        // Act
        PayPeriod period = calendar.Containing(new DateOnly(2026, 9, 26));

        // Assert
        await Assert.That(period.Start).IsEqualTo(new DateOnly(2026, 9, 25));
    }

    [Test]
    public async Task Containing_PayLate_KeepsThePeriodOpen()
    {
        // Arrange: due on Monday 30 Nov, not in by 2 Dec
        PayCalendar calendar = new(PayPattern.LastWorkingDay, Pays, new DateOnly(2026, 12, 2));

        // Act
        PayPeriod period = calendar.Containing(new DateOnly(2026, 12, 2));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(period.Start).IsEqualTo(new DateOnly(2026, 10, 30));
            await Assert.That(period.End).IsEqualTo(new DateOnly(2026, 12, 2));
        }
    }

    [Test]
    public async Task Containing_PayStoppedAWeekAgo_GoesByThePattern()
    {
        // Arrange: due on 30 Nov, nothing by 10 Dec
        PayCalendar calendar = new(PayPattern.LastWorkingDay, Pays, new DateOnly(2026, 12, 10));

        // Act
        PayPeriod period = calendar.Containing(new DateOnly(2026, 12, 10));

        // Assert
        await Assert.That(period.Start).IsEqualTo(new DateOnly(2026, 11, 30));
    }

    [Test]
    public async Task Named_PaidMidMonth_IsNamedAfterThatMonth()
    {
        // Arrange
        PayCalendar calendar = new(PayPattern.OnDay(15), [], new DateOnly(2026, 11, 4));

        // Act
        PayPeriod period = calendar.Named(2026, 10);

        // Assert
        await Assert.That(period).IsEqualTo(new PayPeriod(new DateOnly(2026, 10, 15), new DateOnly(2026, 11, 12),
            2026, 10));
    }

    [Test]
    public async Task CalendarMonths_AreCalendarMonths()
    {
        // Act
        PayPeriod period = PayCalendar.CalendarMonths.Containing(new DateOnly(2026, 11, 4));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(period).IsEqualTo(new PayPeriod(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30),
                2026, 11));
            await Assert.That(PayCalendar.CalendarMonths.FollowsPay).IsFalse();
        }
    }
}
