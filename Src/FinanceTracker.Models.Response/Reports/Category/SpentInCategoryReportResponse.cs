using System.ComponentModel;

namespace FinanceTracker.Models.Response.Reports.Category;

public class SpentInCategoryReportResponse : SharedReportResponse
{
    public string Category { get; set; }

    [Description("Yearly breakdown of spending for the specified time period")]
    public IList<SpentInCategoryReportYearlyBreakdownResponse> YearlyBreakdown { get; set; } = [];
}
