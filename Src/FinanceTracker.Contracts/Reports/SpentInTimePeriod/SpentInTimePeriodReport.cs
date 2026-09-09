using System.ComponentModel;

namespace FinanceTracker.Contracts.Reports.SpentInTimePeriod;

public class SpentInTimePeriodReport : SharedReport
{
    [Description("Yearly breakdown of spending for the specified time period")]
    public IList<SpentInTimePeriodReportYearlyBreakdown> YearlyBreakdown { get; } = [];
}
