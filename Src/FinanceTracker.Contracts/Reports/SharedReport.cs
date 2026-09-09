using System.ComponentModel;

namespace FinanceTracker.Contracts.Reports;

public class SharedReport : IReport
{
    [Description("Total number of transactions")]
    public int TotalTransactions { get; init; }

    [Description("Total amount incoming")]
    public decimal TotalIn { get; init; }

    [Description("Total amount outgoing")]
    public decimal TotalOut { get; init; }
}
