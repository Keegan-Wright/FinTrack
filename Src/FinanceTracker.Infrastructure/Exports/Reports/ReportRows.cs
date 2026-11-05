using System.Globalization;
using FinanceTracker.Contracts.Reports;
using FinanceTracker.Contracts.Reports.Account;
using FinanceTracker.Contracts.Reports.Category;
using FinanceTracker.Contracts.Reports.SpentInTimePeriod;
using FinanceTracker.Infrastructure.Exports.Pdf;

namespace FinanceTracker.Infrastructure.Exports.Reports;

/// <summary>
/// Maps the report service's three shapes onto <see cref="ReportRow"/>s, as the reports page's tree does. Years
/// oldest first, each with all twelve months, those without transactions as zeros.
/// </summary>
internal static class ReportRows
{
    public static ReportRow From(SpentInTimePeriodReport report) =>
        Subject("All accounts", report, report.YearlyBreakdown.Select(static year => Year(year.Year, year,
            year.MonthlyBreakdown.Select(month => Month(year.Year, month.Month, month.Start, month.End, month)))));

    public static ReportRow From(SpentInAccountReport report) =>
        Subject(report.AccountName, report, report.YearlyBreakdown.Select(static year => Year(year.Year, year,
            year.MonthlyBreakdown.Select(month => Month(year.Year, month.Month, month.Start, month.End, month)))));

    public static ReportRow From(SpentInCategoryReport report) =>
        Subject(report.Category, report, report.YearlyBreakdown.Select(static year => Year(year.Year, year,
            year.MonthlyBreakdown.Select(month => Month(year.Year, month.Month, month.Start, month.End, month)))));

    private static ReportRow Subject(string name, IReport totals, IEnumerable<ReportRow> years) => new()
    {
        Title = name,
        Label = name,
        TotalTransactions = totals.TotalTransactions,
        TotalIn = totals.TotalIn,
        TotalOut = totals.TotalOut,
        Periods = years.OrderBy(static x => x.Title, StringComparer.Ordinal).ToList()
    };

    private static ReportRow Year(int year, IReport totals, IEnumerable<(int Number, ReportRow Row)> months)
    {
        Dictionary<int, ReportRow> byNumber = [];
        foreach ((int number, ReportRow row) in months)
        {
            byNumber.TryAdd(number, row);
        }

        string name = year.ToString(CultureInfo.InvariantCulture);
        return new ReportRow
        {
            Title = name,
            Label = name,
            TotalTransactions = totals.TotalTransactions,
            TotalIn = totals.TotalIn,
            TotalOut = totals.TotalOut,
            Periods = Enumerable.Range(1, 12)
                .Select(number => byNumber.GetValueOrDefault(number) ?? EmptyMonth(year, number))
                .ToList()
        };
    }

    /// <param name="monthName">The month the period is named after, in the server's culture, as the report service
    /// names it.</param>
    /// <param name="start">The period's first day, from pay day or the 1st.</param>
    /// <param name="end">Its last day, inclusive.</param>
    private static (int Number, ReportRow Row) Month(int year, string monthName, DateOnly start, DateOnly end,
        IReport totals)
    {
        int number = DateTime.ParseExact(monthName, "MMMM", CultureInfo.CurrentCulture).Month;
        DateOnly named = new(year, number, 1);
        return (number, new ReportRow
        {
            Title = named.ToString("MMMM yyyy", PdfFormat.Culture),
            Label = named.ToString("MMM", PdfFormat.Culture),
            Dates = FromPayDay(named, start, end) ? PdfFormat.DateRange(start, end) : null,
            TotalTransactions = totals.TotalTransactions,
            TotalIn = totals.TotalIn,
            TotalOut = totals.TotalOut
        });
    }

    private static ReportRow EmptyMonth(int year, int number)
    {
        DateOnly named = new(year, number, 1);
        return new ReportRow
        {
            Title = named.ToString("MMMM yyyy", PdfFormat.Culture),
            Label = named.ToString("MMM", PdfFormat.Culture)
        };
    }

    /// <summary>
    /// Whether the period isn't simply the calendar month it's named after.
    /// </summary>
    private static bool FromPayDay(DateOnly named, DateOnly start, DateOnly end) =>
        start != named || end != named.AddMonths(1).AddDays(-1);
}
