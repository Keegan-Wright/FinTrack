using System.ComponentModel;

namespace FinanceTracker.Models.Response.Reports.Account;

public class SpentInAccountReportResponse : SharedReportResponse
{
    [Description("Yearly breakdown of spending for the specified time period")]
    public IList<SpentInAccountReportYearlyBreakdownResponse> YearlyBreakdown { get; set; } = [];

    public string AccountName { get; set; }
}
