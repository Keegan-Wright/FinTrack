using System.ComponentModel;

namespace FinanceTracker.Models.Response.Reports;

public class SharedReportResponse
{
    [Description("Total number of transactions")]
    public int TotalTransactions { get; init; }

    [Description("Total amount incoming")]
    public decimal TotalIn { get; init; }

    [Description("Total amount outgoing")]
    public decimal TotalOut { get; init; }
}
