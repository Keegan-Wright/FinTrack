using System.ComponentModel;

namespace FinanceTracker.Contracts.Dashboard;

/// <summary>
/// Money in and out over a period.
/// </summary>
public class SpentInTimePeriod
{
    /// <summary>
    /// Money in, 0 or more.
    /// </summary>
    [Description("Total amount of money received in the time period")]
    public decimal TotalIn { get; init; }

    /// <summary>
    /// Money out, 0 or less.
    /// </summary>
    [Description("Total amount of money spent in the time period")]
    public decimal TotalOut { get; init; }
}
