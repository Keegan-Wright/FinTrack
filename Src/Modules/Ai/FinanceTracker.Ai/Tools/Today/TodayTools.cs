using System.ComponentModel;
using System.Globalization;

namespace FinanceTracker.Ai.Tools.Today;

/// <summary>
/// Tells a model the date and currency, which it can't know for itself and needs for questions like "this week".
/// </summary>
/// <param name="timeProvider">The clock, whose time zone the day is in.</param>
public sealed class TodayTools(TimeProvider timeProvider)
{
    /// <summary>
    /// Today, the starts of this week, month and year, and the currency amounts are in.
    /// </summary>
    [AiTool("get_today")]
    [Description("Gives today's date, when this week, month and year started, and the currency every amount is in. " +
                 "Call it first whenever a question mentions a relative date, such as this week, last month or the " +
                 "next 10 days, to work out the dates to pass to other tools.")]
    public TodayDetails GetToday()
    {
        DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        DayOfWeek firstDay = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        int daysIntoWeek = ((int)today.DayOfWeek - (int)firstDay + 7) % 7;
        return new TodayDetails(today, today.DayOfWeek, today.AddDays(-daysIntoWeek),
            new DateOnly(today.Year, today.Month, 1), new DateOnly(today.Year, 1, 1), ToolCurrency.Code,
            ToolCurrency.Symbol);
    }
}
