using FinanceTracker.Contracts.Budget;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Tests.Integration.Http;

namespace FinanceTracker.Tests.Integration.Api;

public class BudgetCategoryApiTests : IntegrationTestBase
{
    [Test]
    public async Task AddBudgetCategory_IsListedAndCanBeDeleted()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IBudgetCategoriesService service = ApiFor(user).Get<IBudgetCategoriesService>();
        DateTime goalDate = new(2027, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        BudgetCategory added = await service.AddBudgetCategoryAsync(new AddBudgetCategory
        {
            Name = "Holiday",
            AvailableFunds = 100m,
            MonthlyStart = 25m,
            SavingsGoal = 1000m,
            GoalCompletionDate = goalDate
        }, CancellationToken);
        List<BudgetCategory> listed = await service.GetBudgetItemsAsync(CancellationToken).ToListAsync(CancellationToken);
        bool deleted = await service.DeleteBudgetCategoryAsync(added.Id, CancellationToken);
        bool deletedAgain = await service.DeleteBudgetCategoryAsync(added.Id, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(added.Name).IsEqualTo("Holiday");
            await Assert.That(added.SavingsGoal).IsEqualTo(1000m);
            await Assert.That(listed.Single().GoalCompletionDate).IsEqualTo(goalDate);
            await Assert.That(deleted).IsTrue();
            await Assert.That(deletedAgain).IsFalse();
        }
    }
}
