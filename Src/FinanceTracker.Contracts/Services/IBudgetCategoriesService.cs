using FinanceTracker.Contracts.Budget;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// The signed in user's budget categories and savings goals.
/// </summary>
public interface IBudgetCategoriesService
{
    /// <summary>
    /// All of the user's budget categories.
    /// </summary>
    IAsyncEnumerable<BudgetCategory> GetBudgetItemsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Adds a budget category for the user.
    /// </summary>
    /// <returns>The category as stored.</returns>
    Task<BudgetCategory> AddBudgetCategoryAsync(AddBudgetCategory categoryToAdd,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes one of the user's budget categories.
    /// </summary>
    /// <returns><see langword="false"/> if the user has no category with <paramref name="id"/>.</returns>
    Task<bool> DeleteBudgetCategoryAsync(Guid id, CancellationToken cancellationToken);
}
