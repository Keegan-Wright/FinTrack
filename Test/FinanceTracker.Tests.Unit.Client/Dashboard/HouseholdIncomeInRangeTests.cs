using FinanceTracker.Client.Components.Dashboard;

namespace FinanceTracker.Tests.Unit.Client.Dashboard;

public class HouseholdIncomeInRangeTests
{
    private static readonly DateOnly Today = new(2026, 11, 6);

    [Test]
    public async Task For_ThisMonth_IsATwelfthOfTheYearWhateverItsLength()
    {
        // Act, a pay period or calendar month is a month's pay
        HouseholdIncomeInRange? income = HouseholdIncomeInRange.For(36_000m, DashboardRangePreset.ThisMonth, Today);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(income!.Amount).IsEqualTo(3_000m);
            await Assert.That(income.Period).IsEqualTo("a month");
        }
    }

    [Test]
    public async Task For_Last30Days_IsThirtyDaysOfTheYear()
    {
        // Act
        HouseholdIncomeInRange? income = HouseholdIncomeInRange.For(36_500m, DashboardRangePreset.Last30Days, Today);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(income!.Amount).IsEqualTo(3_000m);
            await Assert.That(income.Period).IsEqualTo("over 30 days");
        }
    }

    [Test]
    [Arguments(2026, 1, 1, 100)]
    [Arguments(2026, 7, 2, 18_300)]
    [Arguments(2026, 12, 31, 36_500)]
    public async Task For_ThisYear_IsTheShareOfTheYearSoFar(int year, int month, int day, int expected)
    {
        // Arrange, 36,500 a year is 100 a day
        DateOnly today = new(year, month, day);

        // Act
        HouseholdIncomeInRange? income = HouseholdIncomeInRange.For(36_500m, DashboardRangePreset.ThisYear, today);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(income!.Amount).IsEqualTo(expected);
            await Assert.That(income.Period).IsEqualTo("so far this year");
        }
    }

    [Test]
    public async Task For_ThisYearInALeapYear_CountsItsExtraDay()
    {
        // Arrange, 36,600 a year is 100 a day in a leap year
        DateOnly lastDay = new(2028, 12, 31);

        // Act
        HouseholdIncomeInRange? income = HouseholdIncomeInRange.For(36_600m, DashboardRangePreset.ThisYear, lastDay);

        // Assert
        await Assert.That(income!.Amount).IsEqualTo(36_600m);
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public async Task For_NoIncomeGiven_IsNothing(int yearly)
    {
        // Act
        HouseholdIncomeInRange? income = HouseholdIncomeInRange.For(yearly, DashboardRangePreset.ThisMonth, Today);

        // Assert
        await Assert.That(income).IsNull();
    }

    [Test]
    public async Task Monthly_IsATwelfth()
    {
        // Act, Assert
        await Assert.That(HouseholdIncomeInRange.Monthly(24_000m)).IsEqualTo(2_000m);
    }
}
