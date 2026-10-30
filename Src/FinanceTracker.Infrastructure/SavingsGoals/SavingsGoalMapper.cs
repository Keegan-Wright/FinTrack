using FinanceTracker.Domain.SavingsGoals;
using SavingsGoal = FinanceTracker.Contracts.SavingsGoals.SavingsGoal;
using SavingsGoalSource = FinanceTracker.Contracts.SavingsGoals.SavingsGoalSource;
using DomainSavingsGoal = FinanceTracker.Domain.SavingsGoals.SavingsGoal;
using DomainSavingsGoalSource = FinanceTracker.Domain.SavingsGoals.SavingsGoalSource;

namespace FinanceTracker.Infrastructure.SavingsGoals;

/// <summary>
/// Between the domain's savings goals and their contracts. The sources share names, so they map by name.
/// </summary>
internal static class SavingsGoalMapper
{
    /// <param name="accountName">The backing account's name, null when it has none or it's been removed.</param>
    public static SavingsGoal ToContract(DomainSavingsGoal goal, SavingsGoalProgress progress, string? accountName) =>
        new()
        {
            Id = goal.Id,
            Name = goal.Name,
            Saved = progress.Saved,
            MonthlyContribution = goal.MonthlyContribution,
            Target = goal.Target,
            TargetDate = goal.TargetDate,
            Source = Enum.Parse<SavingsGoalSource>(goal.Source.ToString()),
            StartingAmount = goal.Saved,
            AccountId = goal.AccountId,
            AccountName = accountName,
            TagIds = goal.TagIds.ToList(),
            TrackedFrom = goal.TrackedFrom,
            CoversNothing = goal.CoversNothing,
            ReachedBy = progress.ReachedBy,
            NeededPerMonth = progress.NeededPerMonth,
            OnTrack = progress.OnTrack
        };

    public static DomainSavingsGoalSource ToDomain(SavingsGoalSource source) =>
        Enum.Parse<DomainSavingsGoalSource>(source.ToString());
}
