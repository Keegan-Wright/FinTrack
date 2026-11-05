using FinanceTracker.Contracts.Reports;
using FinanceTracker.Infrastructure.Exports.Pdf;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using static FinanceTracker.Infrastructure.Exports.Pdf.PdfFormat;
using static FinanceTracker.Infrastructure.Exports.Pdf.PdfTheme;

namespace FinanceTracker.Infrastructure.Exports.Reports;

/// <summary>
/// Lays a <see cref="ReportContent"/> out as the reports page shows it. Over time, the totals, then each year's
/// months as a chart and a table. By account or tag: a table of each, then each one's years the same way.
/// </summary>
public sealed class ReportDocument : FinTrackDocument
{
    /// <summary>
    /// Room for a heading and a chart, so a heading at the foot of a page moves over with its chart.
    /// </summary>
    private const float HeadingAndChart = 190;

    private readonly ReportContent _report;

    public ReportDocument(ReportContent report)
        : base(report.MadeOn)
    {
        _report = report;
    }

    protected override string Title => _report.Breakdown switch
    {
        ReportBreakdown.ByAccount => "Report by account",
        ReportBreakdown.ByTag => "Report by tag",
        _ => "Report over time"
    };

    protected override string Covers
    {
        get
        {
            if (_report.From is { } from && _report.To is { } to)
            {
                return DateRange(from, to);
            }

            if (_report.From is { } start)
            {
                return $"From {LongDate(start)}";
            }

            if (_report.To is { } end)
            {
                return $"Up to {LongDate(end)}";
            }

            return "All time";
        }
    }

    protected override string CoversDetail =>
        _report.Filters.Count == 0 ? "All your transactions" : "The transactions you filtered";

    protected override void ComposeContent(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(20);

            if (_report.Filters.Count > 0)
            {
                column.Item().Component(new NotesPanel("Showing only", _report.Filters));
            }

            if (_report.Subjects.Count == 0)
            {
                column.Item().EmptyNote("No transactions match these filters.");
                return;
            }

            if (_report.Breakdown == ReportBreakdown.OverTime)
            {
                ComposeOverTime(column, _report.Subjects[0]);
                return;
            }

            ComposeBreakdown(column);
        });
    }

    private static void ComposeOverTime(ColumnDescriptor column, ReportRow everything)
    {
        string counted = $"{TransactionCount(everything.TotalTransactions)}. Moves between your own accounts or your " +
                         "household aren't counted.";
        column.Item().Component(new InOutSummary(everything.TotalIn, -everything.TotalOut, "Net", [counted]));

        foreach (ReportRow year in NewestFirst(everything.Periods))
        {
            column.Item().EnsureSpace(HeadingAndChart)
                .Component(new PdfSection(year.Title, Totals(year), x => ComposeYear(x, year)));
        }
    }

    private void ComposeBreakdown(ColumnDescriptor column)
    {
        bool byAccount = _report.Breakdown == ReportBreakdown.ByAccount;
        string caption = byAccount
            ? "Moves between your own accounts are counted, as each account sees them"
            : "A transaction with more than one tag counts towards each, moves between your own accounts aren't";

        column.Item().Component(new PdfSection(byAccount ? "Accounts" : "Tags", caption, ComposeSubjectTable));

        foreach (ReportRow subject in _report.Subjects)
        {
            column.Item().EnsureSpace(HeadingAndChart + 40)
                .Component(new PdfSection(subject.Title, Totals(subject), x => ComposeSubject(x, subject)));
        }
    }

    private void ComposeSubjectTable(IContainer container)
    {
        string nameHeader = _report.Breakdown == ReportBreakdown.ByAccount ? "Account" : "Tag";
        List<TotalsTableRow> rows = _report.Subjects.Select(static x => TableRow(x, detail: null)).ToList();
        container.Component(new TotalsTable(nameHeader, rows));
    }

    private static void ComposeSubject(IContainer container, ReportRow subject)
    {
        container.Column(column =>
        {
            column.Spacing(16);
            foreach (ReportRow year in NewestFirst(subject.Periods))
            {
                column.Item().EnsureSpace(HeadingAndChart).Column(block =>
                {
                    block.Item().Text(year.Title).FontSize(10).SemiBold();
                    block.Item().Text(Totals(year)).FontSize(SmallSize).FontColor(TextSecondary);
                    block.Item().PaddingTop(6).Element(x => ComposeYear(x, year));
                });
            }
        });
    }

    /// <summary>
    /// The year's twelve months as a chart, then those with transactions in a table.
    /// </summary>
    private static void ComposeYear(IContainer container, ReportRow year)
    {
        List<InOutChartBar> bars = year.Periods
            .Select(static x => new InOutChartBar(x.Label, x.TotalIn, -x.TotalOut))
            .ToList();
        List<TotalsTableRow> months = year.Periods
            .Where(static x => x.TotalTransactions > 0)
            .Select(static x => TableRow(x, x.Dates))
            .ToList();

        container.Column(column =>
        {
            column.Item().ShowEntire().Component(new InOutChart(bars));
            column.Item().PaddingTop(10).Component(new TotalsTable("Month", months));
        });
    }

    /// <param name="detail">Under the name, e.g. a pay period's dates, none when null.</param>
    private static TotalsTableRow TableRow(ReportRow row, string? detail) =>
        new(row.Title, detail, row.TotalTransactions, row.TotalIn, -row.TotalOut);

    /// <summary>
    /// e.g. £2,450.00 in, £1,984.37 out, 128 transactions.
    /// </summary>
    private static string Totals(ReportRow row) =>
        $"{Money(row.TotalIn)} in, {Money(-row.TotalOut)} out, {TransactionCount(row.TotalTransactions)}";

    private static IEnumerable<ReportRow> NewestFirst(IReadOnlyList<ReportRow> years) =>
        years.Where(static x => x.TotalTransactions > 0).Reverse();
}
