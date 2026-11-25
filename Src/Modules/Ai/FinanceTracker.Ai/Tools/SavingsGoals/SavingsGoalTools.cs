using System.ComponentModel;
using FinanceTracker.Contracts.SavingsGoals;
using FinanceTracker.Contracts.Services;

namespace FinanceTracker.Ai.Tools.SavingsGoals;

/// <summary>
/// Tools for the user's savings goals.
/// </summary>
/// <param name="savingsGoalsService">Reads the goals, the same as the savings goals page.</param>
/// <param name="pageLinks">Makes the links to the pages its results can be checked on.</param>
public sealed class SavingsGoalTools(ISavingsGoalsService savingsGoalsService, PageLinks pageLinks)
{
    /// <summary>
    /// The user's savings goals.
    /// </summary>
    /// <param name="cancellationToken">Stops the work.</param>
    [AiTool("get_savings_goals")]
    [Description("Lists the user's savings goals: what's saved, the target, and when each will be reached at " +
        "the current rate.")]
    public async Task<SavingsGoalList> GetSavingsGoalsAsync(CancellationToken cancellationToken = default)
    {
        List<SavingsGoalSummary> goals = [];
        await foreach (SavingsGoal goal in savingsGoalsService.GetSavingsGoalsAsync(cancellationToken))
        {
            DateOnly? targetDate = null;
            if (goal.TargetDate is { } target)
            {
                targetDate = DateOnly.FromDateTime(target);
            }

            goals.Add(new SavingsGoalSummary(goal.Name, goal.Saved, goal.Target, targetDate,
                goal.MonthlyContribution, goal.ReachedBy, goal.NeededPerMonth, goal.OnTrack));
        }

        return new SavingsGoalList(goals, pageLinks.For(ToolLinks.SavingsGoals));
    }
}
