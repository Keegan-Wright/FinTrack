using FinanceTracker.Contracts.Budget;
using FinanceTracker.Domain;
using FinanceTracker.Infrastructure.Budget;
using FinanceTracker.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using BudgetCategory = FinanceTracker.Domain.BudgetCategory;

namespace FinanceTracker.Infrastructure.Tests.Services.Budget;

public class BudgetCategoriesServiceTests : ServiceTestsFixtureBase
{
    [Test]
    public async Task AddBudgetCategoryAsync_ThenGetBudgetItemsAsync_ReturnsOnlyCurrentUsersData()
    {
        // Arrange
        string databaseName = $"budget-{Guid.NewGuid()}";
        IDbContextFactory<FinanceTrackerContext> factory = BuildFactory(databaseName);
        await SeedUsersAsync(factory, _cancellationTokenSource.Token);

        await using (FinanceTrackerContext context = await factory.CreateDbContextAsync(_cancellationTokenSource.Token))
        {
            FinanceTrackerUser secondaryUser = await context.Users.Include(x => x.BudgetCategories)
                .SingleAsync(x => x.Id == SecondaryUserId, _cancellationTokenSource.Token);

            secondaryUser.BudgetCategories!.Add(new BudgetCategory
            {
                Name = "Secondary-only",
                AvailableFunds = 9,
                MonthlyStart = 1,
                SavingsGoal = 20,
                GoalCompletionDate = DateTime.UtcNow.Date,
                Created = DateTime.UtcNow
            });

            await context.SaveChangesAsync(_cancellationTokenSource.Token);
        }

        var subject = new BudgetCategoriesService(BuildUser(PrimaryUserId), factory, NullLogger<BudgetCategoriesService>.Instance);

        // Act
        var added = await subject.AddBudgetCategoryAsync(new AddBudgetCategory
        {
            Name = "Emergency",
            AvailableFunds = 100,
            MonthlyStart = 10,
            SavingsGoal = 1000,
            GoalCompletionDate = DateTime.UtcNow.Date.AddMonths(3)
        }, _cancellationTokenSource.Token);

        List<string> names = [];
        await foreach (var item in subject.GetBudgetItemsAsync(_cancellationTokenSource.Token))
        {
            names.Add(item.Name);
        }

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(added.Name).IsEqualTo("Emergency");
            await Assert.That(names).Contains("Emergency");
            await Assert.That(names).DoesNotContain("Secondary-only");
        }
    }

    [Test]
    public async Task DeleteBudgetCategoryAsync_WhenExists_RemovesCategoryAndReturnsTrue()
    {
        // Arrange
        string databaseName = $"budget-delete-{Guid.NewGuid()}";
        IDbContextFactory<FinanceTrackerContext> factory = BuildFactory(databaseName);
        await SeedUsersAsync(factory, _cancellationTokenSource.Token);

        Guid categoryId;
        await using (FinanceTrackerContext context = await factory.CreateDbContextAsync(_cancellationTokenSource.Token))
        {
            FinanceTrackerUser primaryUser = await context.Users.Include(x => x.BudgetCategories)
                .SingleAsync(x => x.Id == PrimaryUserId, _cancellationTokenSource.Token);

            var category = new BudgetCategory
            {
                Name = "Disposable",
                AvailableFunds = 30,
                MonthlyStart = 1,
                SavingsGoal = 40,
                GoalCompletionDate = DateTime.UtcNow.Date.AddDays(7),
                Created = DateTime.UtcNow
            };

            primaryUser.BudgetCategories!.Add(category);
            await context.SaveChangesAsync(_cancellationTokenSource.Token);
            categoryId = category.Id;
        }

        var subject = new BudgetCategoriesService(BuildUser(PrimaryUserId), factory, NullLogger<BudgetCategoriesService>.Instance);

        // Act
        bool deleted = await subject.DeleteBudgetCategoryAsync(categoryId, _cancellationTokenSource.Token);

        // Assert
        await using FinanceTrackerContext verificationContext = await factory.CreateDbContextAsync(_cancellationTokenSource.Token);
        int remaining = await verificationContext.BudgetCategories.CountAsync(_cancellationTokenSource.Token);

        using (Assert.Multiple())
        {
            await Assert.That(deleted).IsTrue();
            await Assert.That(remaining).IsEqualTo(0);
        }
    }

    [Test]
    public async Task GetBudgetItemsAsync_WhenUserHasNoBudgetCategories_ReturnsEmpty()
    {
        string databaseName = $"budget-empty-{Guid.NewGuid()}";
        IDbContextFactory<FinanceTrackerContext> factory = BuildFactory(databaseName);
        await SeedUsersAsync(factory, _cancellationTokenSource.Token);

        var subject = new BudgetCategoriesService(BuildUser(PrimaryUserId), factory, NullLogger<BudgetCategoriesService>.Instance);

        var names = new List<string>();
        await foreach (var item in subject.GetBudgetItemsAsync(_cancellationTokenSource.Token))
        {
            names.Add(item.Name);
        }

        await Assert.That(names.Count).IsEqualTo(0);
    }
}
