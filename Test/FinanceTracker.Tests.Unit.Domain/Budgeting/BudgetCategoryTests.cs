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
    public void BudgetCategory_ChangeMonthlyStart_Negative_Throws_ArgumentOutOfRangeException()
    {
        var category = new BudgetCategory(Guid.NewGuid(), "Test", 0m, 0m, 0m, null);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            category.ChangeMonthlyStart(-1));
    }

    [Test]
    public void BudgetCategory_SetSavingsGoal_Negative_Throws_ArgumentOutOfRangeException()
    {
        var category = new BudgetCategory(Guid.NewGuid(), "Test", 0m, 0m, 0m, null);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            category.SetSavingsGoal(-1, DateTime.UtcNow));
    }

    [Test]
    public void BudgetCategory_AdjustAvailableFunds_Negative_Throws_ArgumentOutOfRangeException()
    {
        var category = new BudgetCategory(Guid.NewGuid(), "Test", 0m, 0m, 0m, null);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            category.SetSavingsGoal(-1, DateTime.UtcNow));
    }

}
