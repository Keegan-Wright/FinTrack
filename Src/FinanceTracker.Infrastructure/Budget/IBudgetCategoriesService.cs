using FinanceTracker.Contracts.Budget;

namespace FinanceTracker.Infrastructure.Budget;

public interface IBudgetCategoriesService
{
    IAsyncEnumerable<BudgetCategory> GetBudgetItemsAsync(CancellationToken cancellationToken);

    Task<BudgetCategory> AddBudgetCategoryAsync(AddBudgetCategory categoryToAdd,
        CancellationToken cancellationToken);

    Task<bool> DeleteBudgetCategoryAsync(Guid id, CancellationToken cancellationToken);
}
