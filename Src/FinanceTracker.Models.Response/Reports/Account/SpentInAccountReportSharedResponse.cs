using System.ComponentModel;

namespace FinanceTracker.Models.Response.Reports.Account;

public class SpentInAccountReportSharedResponse
{
    [Description("Total number of transactions in the time period")]
    public int TotalTransactions { get; set; }

    [Description("Total amount incoming in the time period")]
    public decimal TotalIn { get; set; }
    
    [Description("Total amount outgoing in the time period")]
    public decimal TotalOut { get; set; }
}