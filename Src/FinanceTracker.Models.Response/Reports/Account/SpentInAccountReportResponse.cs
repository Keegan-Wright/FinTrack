using System.ComponentModel;

namespace FinanceTracker.Models.Response.Reports.Account;

public class SpentInAccountReportResponse : SpentInAccountReportSharedResponse
{
    [Description("Yearly breakdown of spending for the specified time period")]
    public IList<SpentInAccountReportYearlyBreakdownResponse> YearlyBreakdown { get; set; } = [];
    
    public string AccountName { get; set; }
}