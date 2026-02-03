using System.Runtime.CompilerServices;
using System.Security.Claims;
using FinanceTracker.Data;
using FinanceTracker.Data.Models;
using FinanceTracker.Models.Request.Budget;
using FinanceTracker.Models.Response.Budget;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Services.Budget;

public class BudgetCategoriesService : ServiceBase, IBudgetCategoriesService
{
    public BudgetCategoriesService(ClaimsPrincipal user, FinanceTrackerContext financeTrackerContext) : base(user, financeTrackerContext)
    {
    }

    public async IAsyncEnumerable<BudgetCategoryResponse> GetBudgetItemsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach(var budgetCategory in _financeTrackerContext.IsolateToUser(UserId)
                          .Include(x => x.BudgetCategories)
                          .SelectMany(x => x.BudgetCategories)
                          .AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return new BudgetCategoryResponse()
            {
                Name =  budgetCategory.Name,
                AvailableFunds = budgetCategory.AvailableFunds,
                MonthlyStart =  budgetCategory.MonthlyStart,
                SavingsGoal = budgetCategory.SavingsGoal,
                GoalCompletionDate = budgetCategory.GoalCompletionDate
            };
        }
    }

    public async Task<BudgetCategoryResponse> AddBudgetCategoryAsync(AddBudgetCategoryRequest categoryToAdd, CancellationToken cancellationToken)
    {
        var user = await _financeTrackerContext.IsolateToUser(UserId)
            .Include(x => x.BudgetCategories).FirstAsync(cancellationToken);
        
        var budgetCategory = new BudgetCategory()
        {
            Name = categoryToAdd.Name,
            AvailableFunds = categoryToAdd.AvailableFunds,
            Created = DateTime.Now.ToUniversalTime(),
            GoalCompletionDate = categoryToAdd.GoalCompletionDate,
            MonthlyStart = categoryToAdd.MonthlyStart,
            SavingsGoal = categoryToAdd.SavingsGoal,
        };
            
        user.BudgetCategories.Add(budgetCategory);

        await _financeTrackerContext.SaveChangesAsync(cancellationToken);

        return new BudgetCategoryResponse()
        {
            Name =  budgetCategory.Name,
            AvailableFunds = budgetCategory.AvailableFunds,
            MonthlyStart =  budgetCategory.MonthlyStart,
            SavingsGoal = budgetCategory.SavingsGoal,
            GoalCompletionDate = budgetCategory.GoalCompletionDate
        };
    }

    public async Task<bool> DeleteBudgetCategoryAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await _financeTrackerContext.IsolateToUser(UserId)
            .Include(x => x.BudgetCategories).SingleAsync(cancellationToken: cancellationToken);
            
        var budgetCategory = user.BudgetCategories.FirstOrDefault(x => x.Id == id);

        if (budgetCategory != null)
        {
            _financeTrackerContext.BudgetCategories.Remove(budgetCategory);
            await _financeTrackerContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        return false;
    }
}