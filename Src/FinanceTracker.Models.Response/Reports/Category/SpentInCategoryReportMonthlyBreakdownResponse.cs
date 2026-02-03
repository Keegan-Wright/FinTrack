using System.ComponentModel;

namespace FinanceTracker.Models.Response.Reports.Category;

public class SpentInCategoryReportMonthlyBreakdownResponse : SpentInCategoryReportSharedResponse
{
    [Description("Month number (1-12) for which the spending breakdown is provided")]
    public string Month { get; set; }
    
    [Description("Daily breakdown of spending for the specified month")]
    public IList<SpentInCategoryReportDailyBreakdownResponse> DailyBreakdown { get; set; } = [];

}