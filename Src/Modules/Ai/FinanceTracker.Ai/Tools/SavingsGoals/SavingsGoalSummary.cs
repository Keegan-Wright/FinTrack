namespace FinanceTracker.Ai.Tools.SavingsGoals;

/// <summary>
/// One of the user's savings goals.
/// </summary>
/// <param name="Name">Its name.</param>
/// <param name="Saved">What's saved so far.</param>
/// <param name="Target">What the user is saving towards.</param>
/// <param name="TargetDate">When they want to reach it, if set.</param>
/// <param name="MonthlyContribution">What they put in a month.</param>
/// <param name="ReachedBy">When it'll be reached at the current rate, null when it won't be.</param>
/// <param name="NeededPerMonth">What's needed a month to reach it by the target date, null without one.</param>
/// <param name="OnTrack">Whether it'll be reached by the target date, null without one.</param>
public sealed record SavingsGoalSummary(string Name, decimal Saved, decimal Target, DateOnly? TargetDate,
    decimal MonthlyContribution, DateOnly? ReachedBy, decimal? NeededPerMonth, bool? OnTrack);
