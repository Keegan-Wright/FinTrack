namespace FinanceTracker.Ai.Tools.Spending;

/// <summary>
/// Two periods' totals side by side.
/// </summary>
/// <param name="First">The first period.</param>
/// <param name="Second">The second period.</param>
/// <param name="MoneyInChange">The second period's money in less the first's.</param>
/// <param name="MoneyOutChange">The second period's money out less the first's, positive when more was spent.</param>
public sealed record PeriodComparison(PeriodTotals First, PeriodTotals Second, decimal MoneyInChange,
    decimal MoneyOutChange);
