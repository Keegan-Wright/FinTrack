namespace FinanceTracker.Ai.Tools.NetWorth;

/// <summary>
/// Net worth at the end of a month, or on its latest day for the current month.
/// </summary>
/// <param name="Date">The day.</param>
/// <param name="Assets">What the user has.</param>
/// <param name="Liabilities">What they owe, positive.</param>
/// <param name="NetWorth">Assets less liabilities.</param>
public sealed record NetWorthMonth(DateOnly Date, decimal Assets, decimal Liabilities, decimal NetWorth);
