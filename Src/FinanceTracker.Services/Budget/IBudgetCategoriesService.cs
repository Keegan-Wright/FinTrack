using FinanceTracker.Models.Request.Budget;
using FinanceTracker.Models.Response.Budget;

namespace FinanceTracker.Services.Budget;

public interface IBudgetCategoriesService
{
    IAsyncEnumerable<BudgetCategoryResponse> GetBudgetItemsAsync(CancellationToken cancellationToken);

    Task<BudgetCategoryResponse> AddBudgetCategoryAsync(AddBudgetCategoryRequest categoryToAdd, CancellationToken cancellationToken);
    Task<bool> DeleteBudgetCategoryAsync(Guid id, CancellationToken cancellationToken);
}