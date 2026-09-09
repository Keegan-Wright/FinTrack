using System.ComponentModel;

namespace FinanceTracker.Contracts.Reports.Account;

public class SpentInAccountReportMonthlyBreakdown : SharedReport
{
    [Description("Month number (1-12) for which the spending breakdown is provided")]
    public required string Month { get; init; }

    [Description("Daily breakdown of spending for the specified month")]
    public IList<SpentInAccountReportDailyBreakdown> DailyBreakdown { get; } = [];
}
