using FinanceTracker.Domain.Budgeting;

namespace FinanceTracker.Tests.Unit.Domain.Budgeting;

[Category("Domain")]
public class BudgetTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid Shopping = Guid.NewGuid();
    private static readonly BudgetMonth January = new(2026, 1);
    private static readonly BudgetMonth February = new(2026, 2);
    private static readonly BudgetMonth March = new(2026, 3);

    [Test]
    public async Task ChangeLimit_FromAMonth_LeavesTheMonthsBefore()
    {
        // Arrange
        Budget budget = Groceries(400m);

        // Act
        bool changed = budget.ChangeLimit(500m, March);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(changed).IsTrue();
            await Assert.That(budget.LimitFor(January)).IsEqualTo(400m);
            await Assert.That(budget.LimitFor(February)).IsEqualTo(400m);
            await Assert.That(budget.LimitFor(March)).IsEqualTo(500m);
            await Assert.That(budget.LimitFor(new BudgetMonth(2027, 6))).IsEqualTo(500m);
        }
    }

    [Test]
    public async Task ChangeLimit_BeforeALaterChange_ReplacesIt()
    {
        // Arrange
        Budget budget = Groceries(400m);
        budget.ChangeLimit(500m, March);

        // Act
        budget.ChangeLimit(450m, February);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(budget.LimitFor(March)).IsEqualTo(450m);
            await Assert.That(budget.Limits).Count().IsEqualTo(2);
        }
    }

    [Test]
    public async Task ChangeLimit_ToWhatItAlreadyIs_ChangesNothing()
    {
        // Arrange
        Budget budget = Groceries(400m);

        // Act
        bool changed = budget.ChangeLimit(400m, March);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(changed).IsFalse();
            await Assert.That(budget.Limits).Count().IsEqualTo(1);
        }
    }

    [Test]
    public async Task LimitFor_BeforeTheFirstMonth_IsNull() =>
        await Assert.That(Groceries(400m).LimitFor(new BudgetMonth(2025, 12))).IsNull();

    [Test]
    public async Task RemoveTag_ItsLastTag_KeepsTheBudgetCoveringNothing()
    {
        // Arrange
        Budget budget = Groceries(400m);

        // Act
        bool removed = budget.RemoveTag(Shopping);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(removed).IsTrue();
            await Assert.That(budget.CoversNothing).IsTrue();
            await Assert.That(budget.LimitFor(January)).IsEqualTo(400m);
        }
    }

    [Test]
    public void New_NoTags_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            _ = new Budget(UserId, "Groceries", [], 400m, January, RolloverMode.None));

    [Test]
    public void ChangeLimit_Negative_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Groceries(400m).ChangeLimit(-1m, March));

    [Test]
    public void ChangeRolloverMode_Unknown_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Groceries(400m).ChangeRolloverMode((RolloverMode)9));

    private static Budget Groceries(decimal limit) =>
        new(UserId, "Groceries", [Shopping], limit, January, RolloverMode.None);
}
