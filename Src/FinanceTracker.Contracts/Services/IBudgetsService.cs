using FinanceTracker.Contracts.Budgets;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// The signed in user's budgets, monthly limits on what their tags cover, and how each month is going.
/// </summary>
/// <remarks>
/// What's spent is always worked out from the transactions, in the server's time zone. Methods that look up by id
/// throw <see cref="InvalidOperationException"/> when the id doesn't exist or belongs to another user.
/// </remarks>
public interface IBudgetsService
{
    /// <summary>
    /// How the user's budgets are doing in a month, past, current or future, with what no budget covers. A budget
    /// that hadn't started by then isn't included.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The year or month is out of range.</exception>
    Task<BudgetMonthOverview> GetMonthAsync(int year, int month, CancellationToken cancellationToken);

    /// <summary>
    /// All of the user's budgets, by name.
    /// </summary>
    IAsyncEnumerable<Budget> GetBudgetsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// One of the user's budgets.
    /// </summary>
    /// <exception cref="InvalidOperationException">The user has no budget with <paramref name="id"/>.</exception>
    Task<Budget> GetBudgetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a budget for the user, running from the current month. Validate the request with
    /// <see cref="Validation.SaveBudgetValidator"/> first.
    /// </summary>
    /// <returns>The budget as stored, with its new id.</returns>
    /// <exception cref="InvalidOperationException">A tag isn't the user's.</exception>
    Task<Budget> AddBudgetAsync(SaveBudget budget, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces everything about one of the user's budgets. A new limit applies from the current month on, the months
    /// before keep theirs. Validate the request with <see cref="Validation.SaveBudgetValidator"/> first.
    /// </summary>
    /// <exception cref="InvalidOperationException">The user has no budget with <paramref name="id"/>, or a tag isn't
    /// theirs.</exception>
    Task<Budget> UpdateBudgetAsync(Guid id, SaveBudget budget, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes one of the user's budgets. Its transactions keep their tags.
    /// </summary>
    /// <exception cref="InvalidOperationException">The user has no budget with <paramref name="id"/>.</exception>
    Task DeleteBudgetAsync(Guid id, CancellationToken cancellationToken);
}
