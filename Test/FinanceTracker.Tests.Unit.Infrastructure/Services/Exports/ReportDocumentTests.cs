using System.Globalization;
using System.Text;
using FinanceTracker.Contracts.Reports;
using FinanceTracker.Infrastructure.Exports.Reports;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Exports;

public class ReportDocumentTests
{
    private static readonly CultureInfo British = CultureInfo.GetCultureInfo("en-GB");

    [Test]
    [Arguments(ReportBreakdown.OverTime)]
    [Arguments(ReportBreakdown.ByAccount)]
    [Arguments(ReportBreakdown.ByTag)]
    public async Task GeneratePdf_EachBreakdown_IsAPdf(ReportBreakdown breakdown)
    {
        // Act
        byte[] pdf = new ReportDocument(Sample(breakdown)).GeneratePdf();

        // Assert
        await Assert.That(Encoding.ASCII.GetString(pdf, 0, 5)).IsEqualTo("%PDF-");
    }

    [Test]
    public async Task GeneratePdf_NothingMatches_StillLaysOut()
    {
        // Arrange
        ReportContent empty = new()
        {
            Breakdown = ReportBreakdown.ByTag,
            Subjects = [],
            Filters = ["Tags: Holiday"],
            MadeOn = new DateOnly(2026, 11, 4)
        };

        // Act
        int pages = PageCount(new ReportDocument(empty));

        // Assert
        await Assert.That(pages).IsEqualTo(1);
    }

    [Test]
    public async Task GeneratePdf_ManyTags_RunOntoMorePages()
    {
        // Arrange
        ReportContent report = Sample(ReportBreakdown.ByTag, subjects: 12);

        // Act
        int pages = PageCount(new ReportDocument(report));

        // Assert
        await Assert.That(pages).IsGreaterThan(3);
    }

    private static int PageCount(IDocument document) =>
        document.GenerateImages(new ImageGenerationSettings { RasterDpi = 10 }).Count();

    /// <summary>
    /// A report with two years of made-up months for each subject, placeholder names only.
    /// </summary>
    internal static ReportContent Sample(ReportBreakdown breakdown, int subjects = 3)
    {
        string[] names = breakdown switch
        {
            ReportBreakdown.ByAccount => ["Main", "Savings", "Joint", "Credit card"],
            ReportBreakdown.ByTag => ["Rent", "Groceries", "Eating out", "Transport", "Holiday"],
            _ => ["All accounts"]
        };

        int count = breakdown == ReportBreakdown.OverTime ? 1 : subjects;
        List<ReportRow> rows = Enumerable.Range(0, count)
            .Select(x => Subject(names[x % names.Length], scale: 1m / (x + 1)))
            .ToList();

        return new ReportContent
        {
            Breakdown = breakdown,
            Subjects = rows,
            From = new DateOnly(2025, 6, 1),
            Filters = breakdown == ReportBreakdown.ByTag ? ["Accounts: Main, Joint"] : [],
            MadeOn = new DateOnly(2026, 11, 4)
        };
    }

    private static ReportRow Subject(string name, decimal scale)
    {
        List<ReportRow> years = [Year(2025, 6, scale), Year(2026, 1, scale)];
        return new ReportRow
        {
            Title = name,
            Label = name,
            TotalIn = years.Sum(static x => x.TotalIn),
            TotalOut = years.Sum(static x => x.TotalOut),
            TotalTransactions = years.Sum(static x => x.TotalTransactions),
            Periods = years
        };
    }

    /// <param name="firstMonth">The first month with anything in it, those before it are empty.</param>
    private static ReportRow Year(int year, int firstMonth, decimal scale)
    {
        List<ReportRow> months = Enumerable.Range(1, 12)
            .Select(number => Month(year, number, number >= firstMonth, scale))
            .ToList();

        return new ReportRow
        {
            Title = year.ToString(British),
            Label = year.ToString(British),
            TotalIn = months.Sum(static x => x.TotalIn),
            TotalOut = months.Sum(static x => x.TotalOut),
            TotalTransactions = months.Sum(static x => x.TotalTransactions),
            Periods = months
        };
    }

    private static ReportRow Month(int year, int number, bool fromFirstMonth, decimal scale)
    {
        DateOnly named = new(year, number, 1);
        ReportRow empty = new() { Title = named.ToString("MMMM yyyy", British), Label = named.ToString("MMM", British) };
        if (!fromFirstMonth || named > new DateOnly(2026, 11, 1))
        {
            return empty;
        }

        return new ReportRow
        {
            Title = empty.Title,
            Label = empty.Label,
            Dates = $"{named.AddDays(-2).ToString("d MMM", British)} to " +
                    named.AddMonths(1).AddDays(-3).ToString("d MMM yyyy", British),
            TotalIn = Math.Round(2450m * scale, 2),
            TotalOut = -Math.Round((1500m + number * 60m) * scale, 2),
            TotalTransactions = 40 + number
        };
    }
}
