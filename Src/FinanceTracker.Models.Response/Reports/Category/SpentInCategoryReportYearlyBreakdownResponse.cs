using System.ComponentModel;

namespace FinanceTracker.Models.Response.Reports.Category;

public class SpentInCategoryReportYearlyBreakdownResponse : SharedReportResponse
{
    [Description("Year for which the spending breakdown is provided")]
    public int Year { get; set; }

    [Description("Monthly breakdown of spending for the specified year")]
    public IList<SpentInCategoryReportMonthlyBreakdownResponse> MonthlyBreakdown { get; set; } = [];
}