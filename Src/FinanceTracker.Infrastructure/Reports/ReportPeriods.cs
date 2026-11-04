using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.PayPeriods;

namespace FinanceTracker.Infrastructure.Reports;

/// <summary>
/// Splits a report's amounts into years, months and days where the user is. A month is the user's period named after
/// it, see <see cref="PayCalendar"/>, so with pay on 30 October November starts then.
/// </summary>
internal static class ReportPeriods
{
    /// <returns>The years with amounts, in order.</returns>
    public static IReadOnlyList<ReportYear> Group(IEnumerable<ReportAmount> amounts, PayCalendar calendar,
        TimeZoneInfo timeZone)
    {
        List<PlacedAmount> placed = amounts.Select(x => Place(x, calendar, timeZone)).ToList();

        List<ReportYear> years = [];
        IEnumerable<IGrouping<int, PlacedAmount>> byYear =
            placed.GroupBy(static x => x.Period.Year).OrderBy(static x => x.Key);
        foreach (IGrouping<int, PlacedAmount> year in byYear)
        {
            List<ReportMonth> months = [];
            for (int month = 1; month <= 12; month++)
            {
                List<PlacedAmount> inMonth = InMonth(year, month);
                months.Add(Month(year.Key, month, inMonth, calendar));
            }

            years.Add(new ReportYear(year.Key, year.Select(static x => x.Amount).ToList(), months));
        }

        return years;
    }

    private static PlacedAmount Place(ReportAmount amount, PayCalendar calendar, TimeZoneInfo timeZone)
    {
        DateOnly day = amount.OccurredAt.LocalDayIn(timeZone);
        return new PlacedAmount(amount, day, calendar.Containing(day));
    }

    private static List<PlacedAmount> InMonth(IEnumerable<PlacedAmount> year, int month) =>
        year.Where(x => x.Period.Month == month).ToList();

    private static ReportMonth Month(int year, int month, List<PlacedAmount> inMonth, PayCalendar calendar)
    {
        List<ReportDay> days = inMonth
            .GroupBy(static x => x.Day)
            .OrderBy(static x => x.Key)
            .Select(static x => new ReportDay(x.Key, x.Select(static y => y.Amount).ToList()))
            .ToList();

        return new ReportMonth(month, calendar.Named(year, month), inMonth.Select(static x => x.Amount).ToList(),
            days);
    }
}
