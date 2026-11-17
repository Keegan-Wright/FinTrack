namespace FinanceTracker.Domain.SavingsGoals;

/// <summary>
/// What a savings goal had saved at the end of a day, see <see cref="SavingsGoalCalculator.History"/>.
/// </summary>
/// <param name="Date">The day.</param>
/// <param name="Saved">What was saved by the end of it.</param>
public sealed record SavedOn(DateOnly Date, decimal Saved);
