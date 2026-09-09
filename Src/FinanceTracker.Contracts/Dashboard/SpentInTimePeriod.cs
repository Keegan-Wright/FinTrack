using System.ComponentModel;

namespace FinanceTracker.Contracts.Dashboard;

public class SpentInTimePeriod
{
    [Description("Total amount of money received in the time period")]
    public decimal TotalIn { get; init; }

    [Description("Total amount of money spent in the time period")]
    public decimal TotalOut { get; init; }
}
