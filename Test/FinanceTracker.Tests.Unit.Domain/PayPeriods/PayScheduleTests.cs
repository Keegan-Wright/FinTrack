using FinanceTracker.Domain.PayPeriods;

namespace FinanceTracker.Tests.Unit.Domain.PayPeriods;

[Category("Domain")]
public class PayScheduleTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly DateOnly Today = On(11, 4);

    [Test]
    public async Task Update_SamePay_AddsItsNewPayDays()
    {
        // Arrange
        PaySchedule schedule = new(UserId, Pay("ACME LTD", 2400m, On(7, 31), On(8, 28),
            On(9, 30)));

        // Act
        schedule.Update([Pay("ACME LTD", 2450m, On(8, 28), On(9, 30), On(10, 30))]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(schedule.PayDays).Count().IsEqualTo(4);
            await Assert.That(schedule.Amount).IsEqualTo(2450m);
        }
    }

    [Test]
    public async Task Update_PayNoLongerFound_MovesToTheLargestFound()
    {
        // Arrange
        PaySchedule schedule = new(UserId, Pay("OLD JOB", 2000m, On(6, 30)));

        // Act
        schedule.Update([Pay("NEW JOB", 2400m, On(10, 30)), Pay("SIDE GIG", 300m, On(10, 15))]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(schedule.Name).IsEqualTo("NEW JOB");
            await Assert.That(schedule.PayDays).IsEquivalentTo([new DateOnly(2026, 10, 30)]);
        }
    }

    [Test]
    public async Task Update_ChosenPayNotFound_IsKept()
    {
        // Arrange
        PaySchedule schedule = new(UserId, Pay("ACME LTD", 2400m, On(10, 30)));
        schedule.Choose(Pay("SIDE GIG", 300m, On(10, 15)));

        // Act
        schedule.Update([Pay("ACME LTD", 2400m, On(10, 30))]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(schedule.Name).IsEqualTo("SIDE GIG");
            await Assert.That(schedule.IsChosen).IsTrue();
        }
    }

    [Test]
    public async Task Calendar_TurnedOff_IsCalendarMonthsUntilTurnedOn()
    {
        // Arrange
        PaySchedule schedule = new(UserId, Pay("ACME LTD", 2400m, On(10, 30)));

        // Act
        schedule.TurnOff();
        bool offFollowsPay = schedule.Calendar(Today).FollowsPay;
        schedule.TurnOn();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(offFollowsPay).IsFalse();
            await Assert.That(schedule.Calendar(Today).FollowsPay).IsTrue();
        }
    }

    private static DateOnly On(int month, int day) => new(2026, month, day);

    private static DetectedPay Pay(string name, decimal amount, params DateOnly[] payDays) =>
        new(AccountId, name.Replace(" ", string.Empty, StringComparison.Ordinal), name, amount,
            PayPattern.LastWorkingDay, payDays);
}
