using System.ComponentModel;

namespace FinanceTracker.Models.Response.Reports.SpentInTimePeriod;

public class SpentInTimePeriodReportMonthlyBreakdownResponse : SharedReportResponse
{
    [Description("Month number (1-12) for which the spending breakdown is provided")]
    public string Month { get; set; }

    [Description("Daily breakdown of spending for the specified month")]
    public IList<SpentInTimePeriodReportDailyBreakdownResponse> DailyBreakdown { get; set; } = [];
}
