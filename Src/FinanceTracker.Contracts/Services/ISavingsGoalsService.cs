using FinanceTracker.Contracts.SavingsGoals;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// The signed in user's savings goals.
/// </summary>
public interface ISavingsGoalsService
{
    /// <summary>
    /// All of the user's savings goals.
    /// </summary>
    IAsyncEnumerable<SavingsGoal> GetSavingsGoalsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Adds a savings goal for the user.
    /// </summary>
    /// <returns>The goal as stored.</returns>
    Task<SavingsGoal> AddSavingsGoalAsync(AddSavingsGoal goalToAdd,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes one of the user's savings goals.
    /// </summary>
    /// <returns><see langword="false"/> if the user has no goal with <paramref name="id"/>.</returns>
    Task<bool> DeleteSavingsGoalAsync(Guid id, CancellationToken cancellationToken);
}
