using System.Runtime.CompilerServices;
using System.Security.Claims;
using FinanceTracker.Data;
using FinanceTracker.Data.Models;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Models.Request.Budget;
using FinanceTracker.Models.Response.Budget;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Services.Budget;

[InjectionCategory(InjectionCategoryType.Service)]
[Scoped<IBudgetCategoriesService>]
public class BudgetCategoriesService : ServiceBase, IBudgetCategoriesService
{
    public BudgetCategoriesService(ClaimsPrincipal user,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory) : base(user,
        financeTrackerContextFactory)
    {
    }

    public async IAsyncEnumerable<BudgetCategoryResponse> GetBudgetItemsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await _financeTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        await foreach (BudgetCategory budgetCategory in context.IsolateToUser(UserId)
                           .Include(x => x.BudgetCategories)
                           .SelectMany(x => x.BudgetCategories)
                           .AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return new BudgetCategoryResponse
            {
                Name = budgetCategory.Name,
                AvailableFunds = budgetCategory.AvailableFunds,
                MonthlyStart = budgetCategory.MonthlyStart,
                SavingsGoal = budgetCategory.SavingsGoal,
                GoalCompletionDate = budgetCategory.GoalCompletionDate
            };
        }
    }

    public async Task<BudgetCategoryResponse> AddBudgetCategoryAsync(AddBudgetCategoryRequest categoryToAdd,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await _financeTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        FinanceTrackerUser user = await context.IsolateToUser(UserId)
            .Include(x => x.BudgetCategories).FirstAsync(cancellationToken);

        BudgetCategory budgetCategory = new()
        {
            Name = categoryToAdd.Name,
            AvailableFunds = categoryToAdd.AvailableFunds,
            Created = DateTime.Now.ToUniversalTime(),
            GoalCompletionDate = categoryToAdd.GoalCompletionDate,
            MonthlyStart = categoryToAdd.MonthlyStart,
            SavingsGoal = categoryToAdd.SavingsGoal
        };

        user.BudgetCategories.Add(budgetCategory);

        await context.SaveChangesAsync(cancellationToken);

        return new BudgetCategoryResponse
        {
            Name = budgetCategory.Name,
            AvailableFunds = budgetCategory.AvailableFunds,
            MonthlyStart = budgetCategory.MonthlyStart,
            SavingsGoal = budgetCategory.SavingsGoal,
            GoalCompletionDate = budgetCategory.GoalCompletionDate
        };
    }

    public async Task<bool> DeleteBudgetCategoryAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await _financeTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        FinanceTrackerUser user = await context.IsolateToUser(UserId)
            .Include(x => x.BudgetCategories).SingleAsync(cancellationToken);

        BudgetCategory? budgetCategory = user.BudgetCategories.FirstOrDefault(x => x.Id == id);

        if (budgetCategory != null)
        {
            context.BudgetCategories.Remove(budgetCategory);
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }

        return false;
    }
}
