namespace FinanceTracker.Client.Components.NetWorth;

/// <summary>
/// One day on a <see cref="BalanceHistoryChart"/>, its amount in the series it belongs to and null in the other.
/// </summary>
/// <param name="Label">The day as the axis shows it.</param>
/// <param name="Synced">The amount when it came from the bank.</param>
/// <param name="Derived">The amount when it was worked out from transactions.</param>
public sealed record HistoryChartPoint(string Label, decimal? Synced, decimal? Derived);
