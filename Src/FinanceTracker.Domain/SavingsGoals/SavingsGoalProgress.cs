namespace FinanceTracker.Domain.SavingsGoals;

/// <summary>
/// How a <see cref="SavingsGoal"/> is doing, from <see cref="SavingsGoalCalculator"/>.
/// </summary>
/// <param name="Saved">What's saved so far, from the goal's source.</param>
/// <param name="ReachedBy">The month the monthly contribution reaches the target from here, null without a target or
/// a contribution, or when it's already reached.</param>
/// <param name="NeededPerMonth">What it takes each month to reach the target by its date, null without both or when
/// the date has passed.</param>
/// <param name="OnTrack">Whether the monthly contribution reaches the target by its date, null when there's nothing to
/// judge it on.</param>
public sealed record SavingsGoalProgress(decimal Saved, DateOnly? ReachedBy, decimal? NeededPerMonth, bool? OnTrack);
