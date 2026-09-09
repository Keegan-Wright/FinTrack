using System.ComponentModel;

namespace FinanceTracker.Contracts.Reports.SpentInTimePeriod;

public readonly struct SpentInTimePeriodReportDailyBreakdown : IReport
{
    [Description("Total number of transactions")]
    public int TotalTransactions { get; init; }

    [Description("Total amount incoming")]
    public decimal TotalIn { get; init; }

    [Description("Total amount outgoing")]
    public decimal TotalOut { get; init; }

    public int Day { get; init; }
}
