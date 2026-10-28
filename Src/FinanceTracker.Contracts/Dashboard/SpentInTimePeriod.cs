using System.ComponentModel;

namespace FinanceTracker.Contracts.Dashboard;

/// <summary>
/// Money in and out over a period, transfers excluded.
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

    /// <summary>
    /// Money in that was moved between the user's own accounts or within their household, left out of
    /// <see cref="TotalIn"/>. 0 or more.
    /// </summary>
    [Description("Money moved in between the user's own accounts or household, not counted as income")]
    public decimal TransferredIn { get; init; }

    /// <summary>
    /// Money out that was moved between the user's own accounts or within their household, left out of
    /// <see cref="TotalOut"/>. 0 or less.
    /// </summary>
    [Description("Money moved out between the user's own accounts or household, not counted as spending")]
    public decimal TransferredOut { get; init; }
}
