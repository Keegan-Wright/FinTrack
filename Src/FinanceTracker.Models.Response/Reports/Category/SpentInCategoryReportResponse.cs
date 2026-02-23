using System.ComponentModel;

namespace FinanceTracker.Models.Response.Reports.Category;

public class SpentInCategoryReportResponse : SharedReportResponse
{
    public required string Category { get; init; }

    [Description("Yearly breakdown of spending for the specified time period")]
    public IList<SpentInCategoryReportYearlyBreakdownResponse> YearlyBreakdown { get; } = [];
}
