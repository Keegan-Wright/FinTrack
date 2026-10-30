using FinanceTracker.Domain.Budgeting;

namespace FinanceTracker.Tests.Unit.Domain.Budgeting;

[Category("Domain")]
public class BudgetMonthTests
{
    [Test]
    public async Task Of_JustAfterMidnightOnTheFirst_IsTheNewMonthWhereTheUserIs()
    {
        // Arrange, 00:30 on 1 July in London is still 30 June in UTC
        TimeZoneInfo london = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        DateTime utc = new(2026, 6, 30, 23, 30, 0, DateTimeKind.Utc);

        // Act
        BudgetMonth month = BudgetMonth.Of(utc, london);

        // Assert
        await Assert.That(month).IsEqualTo(new BudgetMonth(2026, 7));
    }

    [Test]
    public async Task Next_InDecember_IsJanuaryTheYearAfter() =>
        await Assert.That(new BudgetMonth(2026, 12).Next).IsEqualTo(new BudgetMonth(2027, 1));

    [Test]
    public async Task Comparison_OrdersByYearThenMonth()
    {
        using (Assert.Multiple())
        {
            await Assert.That(new BudgetMonth(2026, 12) < new BudgetMonth(2027, 1)).IsTrue();
            await Assert.That(new BudgetMonth(2026, 3) > new BudgetMonth(2026, 2)).IsTrue();
        }
    }

    [Test]
    [Arguments(0)]
    [Arguments(13)]
    public void New_MonthOutOfRange_Throws(int month) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new BudgetMonth(2026, month));
}
