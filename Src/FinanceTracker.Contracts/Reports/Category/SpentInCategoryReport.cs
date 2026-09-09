using System.ComponentModel;

namespace FinanceTracker.Contracts.Reports.Category;

public class SpentInCategoryReport : SharedReport
{
    public required string Category { get; init; }

    [Description("Yearly breakdown of spending for the specified time period")]
    public IList<SpentInCategoryReportYearlyBreakdown> YearlyBreakdown { get; } = [];
}
