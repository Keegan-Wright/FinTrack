using FinanceTracker.Contracts.Reports;

namespace FinanceTracker.Contracts.Api;

/// <summary>
/// What an export's file is called, the same whether the server names the download or the browser saves it.
/// </summary>
public static class ExportFileNames
{
    /// <summary>
    /// e.g. <c>fintrack-transactions-2026-11-04.csv</c>.
    /// </summary>
    public static string Transactions(DateTimeOffset now) => $"fintrack-transactions-{now:yyyy-MM-dd}.csv";

    /// <summary>
    /// e.g. <c>fintrack-backup-2026-11-04.zip</c>.
    /// </summary>
    public static string Backup(DateTimeOffset now) => $"fintrack-backup-{now:yyyy-MM-dd}.zip";

    /// <summary>
    /// e.g. <c>fintrack-statement-2026-11.pdf</c> for the period named after November 2026.
    /// </summary>
    public static string Statement(int year, int month) => $"fintrack-statement-{year:D4}-{month:D2}.pdf";

    /// <summary>
    /// e.g. <c>fintrack-report-by-tag-2026-11-04.pdf</c>.
    /// </summary>
    public static string Report(ReportBreakdown breakdown, DateTimeOffset now)
    {
        string kind = breakdown switch
        {
            ReportBreakdown.ByAccount => "by-account",
            ReportBreakdown.ByTag => "by-tag",
            _ => "over-time"
        };

        return $"fintrack-report-{kind}-{now:yyyy-MM-dd}.pdf";
    }
}
