using System.Globalization;
using FinanceTracker.Client.Components.Common;
using FinanceTracker.Contracts.SavingsGoals;

namespace FinanceTracker.Client.Components.SavingsGoals;

/// <summary>
/// How savings goals read on screen what's saved, where it comes from and whether it's on track.
/// </summary>
public static class SavingsGoalText
{
    /// <summary>
    /// e.g. <c>£1,300.00 of £10,000.00</c>, or just what's saved without a target.
    /// </summary>
    public static string SavedOf(SavingsGoal goal) => goal.Target > 0
        ? $"{CurrencyFormatter.Format(goal.Saved)} of {CurrencyFormatter.Format(goal.Target)}"
        : $"{CurrencyFormatter.Format(goal.Saved)} saved";

    /// <summary>
    /// How much of the target is saved, 0 to 1, 0 without a target.
    /// </summary>
    public static double Progress(SavingsGoal goal)
    {
        if (goal.Target <= 0)
        {
            return 0;
        }

        return Math.Clamp((double)(goal.Saved / goal.Target), 0, 1);
    }

    /// <summary>
    /// Where the saved amount comes from, e.g. <c>Balance of Rainy Day</c>.
    /// </summary>
    /// <param name="tagNames">The user's tags by id.</param>
    public static string Source(SavingsGoal goal, IReadOnlyDictionary<Guid, string> tagNames)
    {
        switch (goal.Source)
        {
            case SavingsGoalSource.Account:
                return $"Balance of {goal.AccountName ?? "a removed account"}";
            case SavingsGoalSource.Tags:
                string tags = string.Join(", ", goal.TagIds.Select(tagNames.GetValueOrDefault).OfType<string>());
                return $"Tagged {tags} since {Day(goal.TrackedFrom)}";
            default:
                return "Typed in";
        }
    }

    /// <summary>
    /// Whether the monthly contribution gets there, null when there's nothing to say.
    /// </summary>
    public static string? Pace(SavingsGoal goal)
    {
        if (goal.Target > 0 && goal.Saved >= goal.Target)
        {
            return "Reached";
        }

        if (goal.OnTrack == false && goal.NeededPerMonth is { } needed && goal.TargetDate is { } targetDate)
        {
            return $"Needs {CurrencyFormatter.Format(needed)} a month to make {Month(targetDate)}";
        }

        if (goal.ReachedBy is { } reachedBy)
        {
            string reaches = $"{CurrencyFormatter.Format(goal.MonthlyContribution)} a month reaches it by " +
                             Month(reachedBy.ToDateTime(TimeOnly.MinValue));
            return goal.OnTrack == true ? $"On track, {reaches}" : reaches;
        }

        return null;
    }

    /// <summary>
    /// Why the goal can't track anything as it stands, null when it can.
    /// </summary>
    public static string? Problem(SavingsGoal goal)
    {
        if (goal.CoversNothing)
        {
            return "Its tags were deleted, edit it to choose one";
        }

        if (goal.Source == SavingsGoalSource.Account && goal.AccountName is null)
        {
            return "Its account was removed, edit it to choose another";
        }

        return null;
    }

    public static string SourceName(SavingsGoalSource source) => source switch
    {
        SavingsGoalSource.Account => "A savings account's balance",
        SavingsGoalSource.Tags => "Tagged transactions",
        _ => "I'll type it in"
    };

    /// <summary>
    /// For screen readers, e.g. <c>Car, £1,300.00 of £10,000.00</c>.
    /// </summary>
    public static string BarLabel(SavingsGoal goal) => $"{goal.Name}, {SavedOf(goal)}";

    private static string Month(DateTime date) => date.ToString("MMM yyyy", CultureInfo.CurrentCulture);

    private static string Day(DateOnly? day) =>
        day?.ToString("d MMM yyyy", CultureInfo.CurrentCulture) ?? string.Empty;
}
