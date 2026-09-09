using System.ComponentModel;

namespace FinanceTracker.Contracts.Reports.Category;

public class SpentInCategoryReportYearlyBreakdownResponse : SharedReportResponse
{
    [Description("Year for which the spending breakdown is provided")]
    public int Year { get; init; }

    [Description("Monthly breakdown of spending for the specified year")]
    public IList<SpentInCategoryReportMonthlyBreakdownResponse> MonthlyBreakdown { get; } = [];
}
