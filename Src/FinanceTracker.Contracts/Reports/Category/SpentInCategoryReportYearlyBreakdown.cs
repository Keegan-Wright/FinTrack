using System.ComponentModel;

namespace FinanceTracker.Contracts.Reports.Category;

/// <summary>
/// Totals for one year.
/// </summary>
public class SpentInCategoryReportYearlyBreakdown : SharedReport
{
    /// <summary>
    /// The year.
    /// </summary>
    [Description("Year for which the spending breakdown is provided")]
    public int Year { get; init; }

    /// <summary>
    /// Each month of the year with transactions.
    /// </summary>
    [Description("Monthly breakdown of spending for the specified year")]
    public IList<SpentInCategoryReportMonthlyBreakdown> MonthlyBreakdown { get; } = [];
}
