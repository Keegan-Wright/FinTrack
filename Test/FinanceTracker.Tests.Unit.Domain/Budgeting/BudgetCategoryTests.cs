using FinanceTracker.Domain.Budgeting;

namespace FinanceTracker.Tests.Unit.Domain.Budgeting;

[Category("Domain")]
public class BudgetCategoryTests
{

    [Test]
    [Arguments("", 100, 100, 1000)]
    [Arguments("Test", -1, 100, 1000)]
    [Arguments("Test", 100, -1, 1000)]
    [Arguments("Test", 100, 100, -1)]
    public void BudgetCategory_InvalidConstruction_Throws_ArgumentException(string name, decimal availableFunds, decimal monthlyStart, decimal savingsGoal)
    {
        Assert.Throws<ArgumentException>(() =>
            new BudgetCategory(Guid.NewGuid(), name, availableFunds, monthlyStart, savingsGoal, null));
    }

    [Test]
    public void BudgetCategory_Rename_EmptyString_Throws_ArgumentException()
    {
        var category = new BudgetCategory(Guid.NewGuid(), "Test", 0m, 0m, 0m, null);

        Assert.Throws<ArgumentException>(() =>
            category.Rename(""));
    }

    [Test]
    public async Task BudgetCategory_Rename_ValidString_DoesNotThrow()
    {
        var category = new BudgetCategory(Guid.NewGuid(), "Test", 0m, 0m, 0m, null);
        var newName = "Testing Category";

        category.Rename(newName);

        await Assert.That(category.Name).IsEqualTo(newName);
    }

    [Test]
    public void BudgetCategory_ChangeMonthlyStart_Negative_Throws_ArgumentOutOfRangeException()
    {
        var category = new BudgetCategory(Guid.NewGuid(), "Test", 0m, 0m, 0m, null);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            category.ChangeMonthlyStart(-1));
    }


    [Test]
    public async Task BudgetCategory_ChangeMonthlyStart_ValidDecimal_DoesNotThrow()
    {
        var category = new BudgetCategory(Guid.NewGuid(), "Test", 0m, 0m, 0m, null);
        var newMonthlyStart =  200m;

        category.ChangeMonthlyStart(newMonthlyStart);

        await Assert.That(category.MonthlyStart).IsEqualTo(newMonthlyStart);
    }


    [Test]
    public void BudgetCategory_SetSavingsGoal_Negative_Throws_ArgumentOutOfRangeException()
    {
        var category = new BudgetCategory(Guid.NewGuid(), "Test", 0m, 0m, 0m, null);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            category.SetSavingsGoal(-1, DateTime.UtcNow));
    }

    [Test]
    public async Task BudgetCategory_SetSavingsGoal_ValidDecimalWithDate_DoesNotThrow()
    {
        var category = new BudgetCategory(Guid.NewGuid(), "Test", 0m, 0m, 0m, null);

        var newSavingGoalValue = 200m;
        var newGoalCompletionDate = DateTime.UtcNow;

        category.SetSavingsGoal(newSavingGoalValue, newGoalCompletionDate);

        using (Assert.Multiple())
        {
            await Assert.That(category.SavingsGoal).IsEqualTo(newSavingGoalValue);
            await Assert.That(category.GoalCompletionDate).IsEqualTo(newGoalCompletionDate);
        }
    }


    [Test]
    public async Task BudgetCategory_SetSavingsGoal_ValidDecimalNoDate_DoesNotThrow()
    {
        var category = new BudgetCategory(Guid.NewGuid(), "Test", 0m, 0m, 0m, DateTime.UtcNow);

        var newSavingGoalValue = 200m;

        category.SetSavingsGoal(newSavingGoalValue, null);

        using (Assert.Multiple())
        {
            await Assert.That(category.SavingsGoal).IsEqualTo(newSavingGoalValue);
            await Assert.That(category.GoalCompletionDate).IsNull();
        }
    }


    [Test]
    public void BudgetCategory_AdjustAvailableFunds_Negative_Throws_ArgumentOutOfRangeException()
    {
        var category = new BudgetCategory(Guid.NewGuid(), "Test", 0m, 0m, 0m, null);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            category.AdjustAvailableFunds(-1));
    }

    [Test]
    public async Task BudgetCategory_AdjustAvailableFunds_ValidDecimalNoDate_DoesNotThrow()
    {
        var category = new BudgetCategory(Guid.NewGuid(), "Test", 0m, 0m, 0m, DateTime.UtcNow);

        var newAvailableFunds = 200m;

        category.AdjustAvailableFunds(newAvailableFunds);


       await Assert.That(category.AvailableFunds).IsEqualTo(newAvailableFunds);
    }

    [Test]
    public async Task BudgetCategory_AdjustAvailableFunds_AddsToExistingFunds()
    {
        var category = new BudgetCategory(Guid.NewGuid(), "Test", 50m, 0m, 0m, null);

        category.AdjustAvailableFunds(25m);
        category.AdjustAvailableFunds(0m);

        await Assert.That(category.AvailableFunds).IsEqualTo(75m);
    }

    [Test]
    public async Task BudgetCategory_ValidConstruction_SetsProperties()
    {
        var userId = Guid.NewGuid();
        var goalDate = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var category = new BudgetCategory(userId, "Holiday", 100m, 25m, 1000m, goalDate);

        using (Assert.Multiple())
        {
            await Assert.That(category.UserId).IsEqualTo(userId);
            await Assert.That(category.Name).IsEqualTo("Holiday");
            await Assert.That(category.AvailableFunds).IsEqualTo(100m);
            await Assert.That(category.MonthlyStart).IsEqualTo(25m);
            await Assert.That(category.SavingsGoal).IsEqualTo(1000m);
            await Assert.That(category.GoalCompletionDate).IsEqualTo(goalDate);
        }
    }

    [Test]
    public async Task BudgetCategory_ZeroValues_AreValid()
    {
        var category = new BudgetCategory(Guid.NewGuid(), "Test", 0m, 0m, 0m, null);

        using (Assert.Multiple())
        {
            await Assert.That(category.AvailableFunds).IsEqualTo(0m);
            await Assert.That(category.MonthlyStart).IsEqualTo(0m);
            await Assert.That(category.SavingsGoal).IsEqualTo(0m);
        }
    }

    [Test]
    [Arguments(null)]
    [Arguments(" ")]
    public void BudgetCategory_Rename_NullOrWhitespace_Throws_ArgumentException(string? name)
    {
        var category = new BudgetCategory(Guid.NewGuid(), "Test", 0m, 0m, 0m, null);

        Assert.Throws<ArgumentException>(() =>
            category.Rename(name!));
    }

    [Test]
    public void BudgetCategory_EmptyUserId_Throws_ArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BudgetCategory(Guid.Empty, "Test", 0m, 0m, 0m, null));
    }

}
