using MudBlazor;

namespace FinanceTracker.Client.Components.Transactions;

/// <summary>
/// <see cref="DateRangeField"/>'s quick picks, relative to today.
/// </summary>
public static class DateRangePresets
{
    /// <summary>
    /// This month, last month, the last 3 months and this year, each ending today (last month ends on its last day).
    /// </summary>
    public static IReadOnlyList<(string Label, DateRange Range)> For(DateTime today)
    {
        DateTime day = today.Date;
        DateTime monthStart = new(day.Year, day.Month, 1);
        return
        [
            ("This month", new DateRange(monthStart, day)),
            ("Last month", new DateRange(monthStart.AddMonths(-1), monthStart.AddDays(-1))),
            ("Last 3 months", new DateRange(day.AddMonths(-3), day)),
            ("This year", new DateRange(new DateTime(day.Year, 1, 1), day))
        ];
    }
}
