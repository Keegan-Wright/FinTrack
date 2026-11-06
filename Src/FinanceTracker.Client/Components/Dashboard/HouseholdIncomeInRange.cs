namespace FinanceTracker.Client.Components.Dashboard;

/// <summary>
/// The household's income for the range the dashboard shows, from what its members earn a year, so it can be set
/// against the same range's money out. A month is a twelfth of the year, the other ranges their share of it by days.
/// </summary>
/// <param name="Amount">The income for the range.</param>
/// <param name="Period">How the range reads after the amount, e.g. <c>a month</c>.</param>
/// <remarks>
/// A record, so <see cref="DashboardData"/> carries it from the prerender to the browser as JSON.
/// </remarks>
public sealed record HouseholdIncomeInRange(decimal Amount, string Period)
{
    private const int MonthsInAYear = 12;
    private const int LastDays = 30;

    /// <summary>
    /// The share of <paramref name="yearlyIncome"/> for <paramref name="preset"/>, the range running to
    /// <paramref name="today"/>.
    /// </summary>
    /// <returns><see langword="null"/> when the household has no income given.</returns>
    public static HouseholdIncomeInRange? For(decimal yearlyIncome, DashboardRangePreset preset, DateOnly today)
    {
        if (yearlyIncome <= 0)
        {
            return null;
        }

        if (preset == DashboardRangePreset.Last30Days)
        {
            return new HouseholdIncomeInRange(yearlyIncome * LastDays / DaysIn(today.Year), $"over {LastDays} days");
        }

        if (preset == DashboardRangePreset.ThisYear)
        {
            return new HouseholdIncomeInRange(yearlyIncome * today.DayOfYear / DaysIn(today.Year), "so far this year");
        }

        // This month, a calendar month or a pay period, is a month's pay whatever its length
        return new HouseholdIncomeInRange(yearlyIncome / MonthsInAYear, "a month");
    }

    /// <summary>
    /// What <paramref name="yearlyIncome"/> is a month.
    /// </summary>
    public static decimal Monthly(decimal yearlyIncome) => yearlyIncome / MonthsInAYear;

    private static int DaysIn(int year) => DateTime.IsLeapYear(year) ? 366 : 365;
}
