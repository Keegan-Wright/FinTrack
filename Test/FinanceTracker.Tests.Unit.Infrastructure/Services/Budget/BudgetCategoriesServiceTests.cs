using FinanceTracker.Contracts.Budget;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Budget;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using BudgetCategory = FinanceTracker.Contracts.Budget.BudgetCategory;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Budget;

public class BudgetCategoriesServiceTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";

    [Test]
    public async Task AddBudgetCategoryAsync_ValidRequest_PersistsForCurrentUserAndReturnsMapped()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        BudgetCategoriesService subject = BuildSubject(Username);
        DateTime goalDate = DateTime.UtcNow.Date.AddMonths(3);

        // Act
        BudgetCategory added = await subject.AddBudgetCategoryAsync(new AddBudgetCategory
        {
            Name = "Emergency",
            AvailableFunds = 100,
            MonthlyStart = 10,
            SavingsGoal = 1000,
            GoalCompletionDate = goalDate
        }, CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Domain.Budgeting.BudgetCategory persisted = await context.BudgetCategories.SingleAsync(CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(added.Name).IsEqualTo("Emergency");
            await Assert.That(added.AvailableFunds).IsEqualTo(100m);
            await Assert.That(added.MonthlyStart).IsEqualTo(10m);
            await Assert.That(added.SavingsGoal).IsEqualTo(1000m);
            await Assert.That(added.GoalCompletionDate).IsEqualTo(goalDate);
            await Assert.That(persisted.UserId).IsEqualTo(Scenario.UserMappings[Username]);
            await Assert.That(persisted.Name).IsEqualTo("Emergency");
        }
    }

    [Test]
    public async Task AddBudgetCategoryAsync_InvalidName_ThrowsAndPersistsNothing()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        BudgetCategoriesService subject = BuildSubject(Username);

        // Act
        await Assert.ThrowsAsync<ArgumentException>(() => subject.AddBudgetCategoryAsync(new AddBudgetCategory
        {
            Name = " ",
            AvailableFunds = 0,
            MonthlyStart = 0,
            SavingsGoal = 0,
            GoalCompletionDate = null
        }, CancellationToken));

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        await Assert.That(await context.BudgetCategories.CountAsync(CancellationToken)).IsEqualTo(0);
    }

    [Test]
    public async Task GetBudgetItemsAsync_ReturnsOnlyCurrentUsersCategories()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BudgetCategoryScenario.WithBudgetCategory("mine", Username, userId =>
                new Domain.Budgeting.BudgetCategory(userId, "Mine", 1, 2, 3, null)))
            .Do(s => s.BudgetCategoryScenario.WithBudgetCategory("theirs", OtherUsername, userId =>
                new Domain.Budgeting.BudgetCategory(userId, "Theirs", 1, 2, 3, null)));

        BudgetCategoriesService subject = BuildSubject(Username);

        // Act
        List<BudgetCategory> items = await subject.GetBudgetItemsAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert
        await Assert.That(items.Select(x => x.Name)).IsEquivalentTo(["Mine"]);
    }

    [Test]
    public async Task GetBudgetItemsAsync_MapsAllFields()
    {
        // Arrange
        DateTime goalDate = new(2027, 1, 31, 0, 0, 0, DateTimeKind.Utc);
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BudgetCategoryScenario.WithBudgetCategory("holiday", Username, userId =>
                new Domain.Budgeting.BudgetCategory(userId, "Holiday", 250.50m, 75m, 2000m, goalDate)));

        BudgetCategoriesService subject = BuildSubject(Username);

        // Act
        BudgetCategory item = (await subject.GetBudgetItemsAsync(CancellationToken).ToListAsync(CancellationToken)).Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(item.Name).IsEqualTo("Holiday");
            await Assert.That(item.AvailableFunds).IsEqualTo(250.50m);
            await Assert.That(item.MonthlyStart).IsEqualTo(75m);
            await Assert.That(item.SavingsGoal).IsEqualTo(2000m);
            await Assert.That(item.GoalCompletionDate).IsEqualTo(goalDate);
        }
    }

    [Test]
    public async Task GetBudgetItemsAsync_WhenUserHasNoBudgetCategories_ReturnsEmpty()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        BudgetCategoriesService subject = BuildSubject(Username);

        // Act
        List<BudgetCategory> items = await subject.GetBudgetItemsAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert
        await Assert.That(items.Count).IsEqualTo(0);
    }

    [Test]
    public async Task DeleteBudgetCategoryAsync_WhenExists_RemovesCategoryAndReturnsTrue()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BudgetCategoryScenario.WithBudgetCategory("disposable", Username))
            .Do(s => s.BudgetCategoryScenario.WithBudgetCategory("kept", Username));

        BudgetCategoriesService subject = BuildSubject(Username);

        // Act
        bool deleted = await subject.DeleteBudgetCategoryAsync(Scenario.BudgetCategoryMappings["disposable"],
            CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        List<Guid> remaining = await context.BudgetCategories.Select(x => x.Id).ToListAsync(CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(deleted).IsTrue();
            await Assert.That(remaining).IsEquivalentTo([Scenario.BudgetCategoryMappings["kept"]]);
        }
    }

    [Test]
    public async Task DeleteBudgetCategoryAsync_WhenNotFound_ReturnsFalse()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        BudgetCategoriesService subject = BuildSubject(Username);

        // Act
        bool deleted = await subject.DeleteBudgetCategoryAsync(Guid.NewGuid(), CancellationToken);

        // Assert
        await Assert.That(deleted).IsFalse();
    }

    [Test]
    public async Task DeleteBudgetCategoryAsync_WhenOwnedByAnotherUser_ReturnsFalseAndKeepsCategory()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BudgetCategoryScenario.WithBudgetCategory("theirs", OtherUsername));

        BudgetCategoriesService subject = BuildSubject(Username);

        // Act
        bool deleted = await subject.DeleteBudgetCategoryAsync(Scenario.BudgetCategoryMappings["theirs"],
            CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(deleted).IsFalse();
            await Assert.That(await context.BudgetCategories.CountAsync(CancellationToken)).IsEqualTo(1);
        }
    }

    private BudgetCategoriesService BuildSubject(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<BudgetCategoriesService>.Instance);
}
