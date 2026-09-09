using System.ComponentModel;

namespace FinanceTracker.Contracts.Reports.Category;

public class SpentInCategoryReportYearlyBreakdown : SharedReport
{
    [Description("Year for which the spending breakdown is provided")]
    public int Year { get; init; }

    [Description("Monthly breakdown of spending for the specified year")]
    public IList<SpentInCategoryReportMonthlyBreakdown> MonthlyBreakdown { get; } = [];
}
