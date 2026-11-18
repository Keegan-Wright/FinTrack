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
            year.MonthlyBreakdown.Select(month => Month(year.Year, month.MonthNumber, month.Start, month.End, month)))));

    public static ReportRow From(SpentInAccountReport report) =>
        Subject(report.AccountName, report, report.YearlyBreakdown.Select(static year => Year(year.Year, year,
            year.MonthlyBreakdown.Select(month => Month(year.Year, month.MonthNumber, month.Start, month.End, month)))));

    public static ReportRow From(SpentInCategoryReport report) =>
        Subject(report.Category, report, report.YearlyBreakdown.Select(static year => Year(year.Year, year,
            year.MonthlyBreakdown.Select(month => Month(year.Year, month.MonthNumber, month.Start, month.End, month)))));

    /// <summary>
    /// The line for everything, an account or a tag.
    /// </summary>
    /// <param name="name">What it's about.</param>
    /// <param name="totals">Its totals.</param>
    /// <param name="years">Its years.</param>
    private static ReportRow Subject(string name, IReport totals, IEnumerable<ReportRow> years) => new()
    {
        Title = name,
        Label = name,
        TotalTransactions = totals.TotalTransactions,
        TotalIn = totals.TotalIn,
        TotalOut = totals.TotalOut,
        Periods = years.OrderBy(static x => x.Title, StringComparer.Ordinal).ToList()
    };

    /// <summary>
    /// A year's line, with every month in it.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="totals">Its totals.</param>
    /// <param name="months">Its months that have transactions.</param>
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

    /// <param name="year">The year.</param>
    /// <param name="number">The month of the year the period is named after, 1 to 12.</param>
    /// <param name="start">The period's first day, from pay day or the 1st.</param>
    /// <param name="end">Its last day, inclusive.</param>
    /// <param name="totals">The period's totals.</param>
    private static (int Number, ReportRow Row) Month(int year, int number, DateOnly start, DateOnly end,
        IReport totals)
    {
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

    /// <summary>
    /// A calendar month with no transactions.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="number">The month of the year, 1 to 12.</param>
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
    /// <param name="named">The first day of the month it's named after.</param>
    /// <param name="start">The period's first day.</param>
    /// <param name="end">The period's last day.</param>
    private static bool FromPayDay(DateOnly named, DateOnly start, DateOnly end) =>
        start != named || end != named.AddMonths(1).AddDays(-1);
}
