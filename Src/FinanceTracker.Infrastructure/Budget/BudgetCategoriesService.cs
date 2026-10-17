using System.Runtime.CompilerServices;
using System.Security.Claims;
using FinanceTracker.Contracts.Budget;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BudgetCategory = FinanceTracker.Contracts.Budget.BudgetCategory;

namespace FinanceTracker.Infrastructure.Budget;

[InjectionCategory(InjectionCategoryType.Service)]
[Scoped<IBudgetCategoriesService>]
public class BudgetCategoriesService : ServiceBase<BudgetCategoriesService>, IBudgetCategoriesService
{
    public BudgetCategoriesService(ClaimsPrincipal user,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<BudgetCategoriesService> logger) : base(user,
        financeTrackerContextFactory, logger)
    {
    }

    public async IAsyncEnumerable<BudgetCategory> GetBudgetItemsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        await foreach (Domain.Budgeting.BudgetCategory budgetCategory in context.BudgetCategories.OwnedBy(UserId)
                           .AsNoTracking()
                           .AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return new BudgetCategory
            {
                Name = budgetCategory.Name,
                AvailableFunds = budgetCategory.AvailableFunds,
                MonthlyStart = budgetCategory.MonthlyStart,
                SavingsGoal = budgetCategory.SavingsGoal,
                GoalCompletionDate = budgetCategory.GoalCompletionDate
            };
        }
    }

    public async Task<BudgetCategory> AddBudgetCategoryAsync(AddBudgetCategory categoryToAdd,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        Domain.Budgeting.BudgetCategory budgetCategory = new(UserId, categoryToAdd.Name,
            categoryToAdd.AvailableFunds, categoryToAdd.MonthlyStart, categoryToAdd.SavingsGoal,
            categoryToAdd.GoalCompletionDate);

        context.BudgetCategories.Add(budgetCategory);

        await context.SaveChangesAsync(cancellationToken);

        return new BudgetCategory
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
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        Domain.Budgeting.BudgetCategory? budgetCategory = await context.BudgetCategories.OwnedBy(UserId)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (budgetCategory == null)
        {
            return false;
        }

        context.BudgetCategories.Remove(budgetCategory);
        await context.SaveChangesAsync(cancellationToken);
        return true;

    }
}
