using System.ComponentModel;

namespace FinanceTracker.Models.Response.Reports.Category;

public readonly struct SpentInCategoryReportDailyBreakdownResponse : IReportResponse
{
    [Description("Total number of transactions")]
    public int TotalTransactions { get; init; }

    [Description("Total amount incoming")]
    public decimal TotalIn { get; init; }

    [Description("Total amount outgoing")]
    public decimal TotalOut { get; init; }

    public int Day { get; init; }
}
