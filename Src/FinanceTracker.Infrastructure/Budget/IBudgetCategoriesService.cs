using FinanceTracker.Contracts.Budget;

namespace FinanceTracker.Infrastructure.Budget;

public interface IBudgetCategoriesService
{
    IAsyncEnumerable<BudgetCategoryResponse> GetBudgetItemsAsync(CancellationToken cancellationToken);

    Task<BudgetCategoryResponse> AddBudgetCategoryAsync(AddBudgetCategoryRequest categoryToAdd,
        CancellationToken cancellationToken);

    Task<bool> DeleteBudgetCategoryAsync(Guid id, CancellationToken cancellationToken);
}
