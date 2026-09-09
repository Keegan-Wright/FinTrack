using System.ComponentModel;

namespace FinanceTracker.Contracts.Reports.Account;

public class SpentInAccountReportYearlyBreakdown : SharedReport
{
    [Description("Year for which the spending breakdown is provided")]
    public int Year { get; init; }

    [Description("Monthly breakdown of spending for the specified year")]
    public IList<SpentInAccountReportMonthlyBreakdown> MonthlyBreakdown { get; } = [];
}
