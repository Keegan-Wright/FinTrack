using System.ComponentModel;

namespace FinanceTracker.Contracts.Reports.Account;

public class SpentInAccountReport : SharedReport
{
    [Description("Yearly breakdown of spending for the specified time period")]
    public IList<SpentInAccountReportYearlyBreakdown> YearlyBreakdown { get; } = [];

    public required string AccountName { get; init; }
}
