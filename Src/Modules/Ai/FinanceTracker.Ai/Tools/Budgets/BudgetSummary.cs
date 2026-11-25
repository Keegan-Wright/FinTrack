namespace FinanceTracker.Ai.Tools.Budgets;

/// <summary>
/// How one budget is doing this month.
/// </summary>
/// <param name="Name">The budget's name.</param>
/// <param name="Available">What can be spent this month, its limit plus anything carried in.</param>
/// <param name="Spent">What's been spent so far, positive.</param>
/// <param name="Remaining">What's left, negative when overspent.</param>
/// <param name="OnPace">Whether spending is at or below where it should be by now.</param>
public sealed record BudgetSummary(string Name, decimal Available, decimal Spent, decimal Remaining, bool OnPace);
