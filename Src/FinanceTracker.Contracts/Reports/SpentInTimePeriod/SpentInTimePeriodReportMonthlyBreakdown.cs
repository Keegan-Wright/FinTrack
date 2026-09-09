using System.ComponentModel;

namespace FinanceTracker.Contracts.Reports.SpentInTimePeriod;

public class SpentInTimePeriodReportMonthlyBreakdown : SharedReport
{
    [Description("Month number (1-12) for which the spending breakdown is provided")]
    public required string Month { get; init; }

    [Description("Daily breakdown of spending for the specified month")]
    public IList<SpentInTimePeriodReportDailyBreakdown> DailyBreakdown { get; } = [];
}
