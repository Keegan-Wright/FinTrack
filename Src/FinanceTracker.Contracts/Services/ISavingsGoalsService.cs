using FinanceTracker.Contracts.SavingsGoals;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// The signed in user's savings goals, and what each has saved from its source typed in, a savings account's balance,
/// or tagged transactions.
/// </summary>
/// <remarks>
/// Methods that look up by id throw <see cref="InvalidOperationException"/> when the id doesn't exist or belongs to
/// another user.
/// </remarks>
public interface ISavingsGoalsService
{
    /// <summary>
    /// All of the user's savings goals by name, with what each has saved and whether it's on track.
    /// </summary>
    IAsyncEnumerable<SavingsGoal> GetSavingsGoalsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// One of the user's savings goals.
    /// </summary>
    /// <exception cref="InvalidOperationException">The user has no goal with <paramref name="id"/>.</exception>
    Task<SavingsGoal> GetSavingsGoalAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// The user's accounts that could back a goal, any but a card, with the goal each already backs.
    /// </summary>
    IAsyncEnumerable<SavingsGoalAccount> GetSavingsAccountsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Adds a savings goal for the user. Validate the request with
    /// <see cref="Validation.SaveSavingsGoalValidator"/> first.
    /// </summary>
    /// <returns>The goal as stored.</returns>
    /// <exception cref="InvalidOperationException">The account isn't the user's, is a card or backs another goal, or
    /// a tag isn't the user's.</exception>
    Task<SavingsGoal> AddSavingsGoalAsync(SaveSavingsGoal goal, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces everything about one of the user's savings goals. Validate the request with
    /// <see cref="Validation.SaveSavingsGoalValidator"/> first.
    /// </summary>
    /// <exception cref="InvalidOperationException">The user has no goal with <paramref name="id"/>, or the account or a
    /// tag can't be used, as for <see cref="AddSavingsGoalAsync"/>.</exception>
    Task<SavingsGoal> UpdateSavingsGoalAsync(Guid id, SaveSavingsGoal goal, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes one of the user's savings goals.
    /// </summary>
    /// <returns><see langword="false"/> if the user has no goal with <paramref name="id"/>.</returns>
    Task<bool> DeleteSavingsGoalAsync(Guid id, CancellationToken cancellationToken);
}
