using System.ComponentModel;

namespace FinanceTracker.Contracts.Dashboard;

public class SpentInTimePeriodRequest
{
    [Description("Start date of the time period to analyze")]
    public required DateTime StartDate { get; init; }

    [Description("End date of the time period to analyze")]
    public required DateTime EndDate { get; init; }
}
