namespace FinanceTracker.Contracts;

/// <summary>
/// Which report a report page shows.
/// </summary>
public enum ReportType
{
    /// <summary>
    /// All transactions together, see <see cref="Services.IReportService.GetSpentInTimePeriodReportAsync"/>.
    /// </summary>
    SpentInTimePeriod,

    /// <summary>
    /// One report per tag, see <see cref="Services.IReportService.GetCategoryBreakdownReportAsync"/>.
    /// </summary>
    CategoryBreakdown,

    /// <summary>
    /// One report per account, see <see cref="Services.IReportService.GetAccountBreakdownReportAsync"/>.
    /// </summary>
    AccountBreakdown
}
