using System.ComponentModel;

namespace FinanceTracker.Contracts.NetWorth;

/// <summary>
/// The user's net worth over time, how it's changed, and what makes it up today.
/// </summary>
public class NetWorthHistory
{
    /// <summary>
    /// The days asked for, oldest first.
    /// </summary>
    [Description("Net worth on each day asked for, oldest first")]
    public List<NetWorthPoint> Points { get; init; } = [];

    /// <summary>
    /// The latest net worth less the last one before this month, null with no history from before it.
    /// </summary>
    [Description("How net worth has changed this month")]
    public decimal? ChangeThisMonth { get; init; }

    /// <summary>
    /// The latest net worth less the last one before this year, null with no history from before it.
    /// </summary>
    [Description("How net worth has changed this year")]
    public decimal? ChangeThisYear { get; init; }

    /// <summary>
    /// The accounts, cards and debts in the latest net worth, largest first.
    /// </summary>
    [Description("The accounts, cards and debts in the latest net worth")]
    public List<NetWorthItem> Breakdown { get; init; } = [];
}
