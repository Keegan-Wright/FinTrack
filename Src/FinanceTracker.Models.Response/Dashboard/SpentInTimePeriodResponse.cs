using System.ComponentModel;

namespace FinanceTracker.Models.Response.Dashboard;

public class SpentInTimePeriodResponse
{
    [Description("Total amount of money received in the time period")]
    public decimal TotalIn { get; set; }

    [Description("Total amount of money spent in the time period")]
    public decimal TotalOut { get; set; }
}
