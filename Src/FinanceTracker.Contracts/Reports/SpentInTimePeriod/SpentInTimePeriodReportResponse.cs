using System.ComponentModel;

namespace FinanceTracker.Contracts.Reports.SpentInTimePeriod;

public class SpentInTimePeriodReportResponse : SharedReportResponse
{
    [Description("Yearly breakdown of spending for the specified time period")]
    public IList<SpentInTimePeriodReportYearlyBreakdownResponse> YearlyBreakdown { get; } = [];
}
