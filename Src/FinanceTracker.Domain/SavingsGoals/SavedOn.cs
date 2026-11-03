namespace FinanceTracker.Domain.SavingsGoals;

/// <summary>
/// What a savings goal had saved at the end of a day, see <see cref="SavingsGoalCalculator.History"/>.
/// </summary>
public sealed record SavedOn(DateOnly Date, decimal Saved);
