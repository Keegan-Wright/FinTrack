using System.ComponentModel;

namespace FinanceTracker.Contracts.Reports.Account;

public class SpentInAccountReportResponse : SharedReportResponse
{
    [Description("Yearly breakdown of spending for the specified time period")]
    public IList<SpentInAccountReportYearlyBreakdownResponse> YearlyBreakdown { get; } = [];

    public required string AccountName { get; init; }
}
