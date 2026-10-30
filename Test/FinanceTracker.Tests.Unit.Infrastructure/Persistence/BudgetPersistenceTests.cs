using FinanceTracker.Domain.Budgeting;
using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Unit.Infrastructure.Persistence;

public class BudgetPersistenceTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private static readonly Guid TagId = Guid.CreateVersion7();
    private static readonly BudgetMonth January = new(2026, 1);
    private static readonly BudgetMonth March = new(2026, 3);

    [Test]
    public async Task Budget_RoundTripsThroughTheDatabase()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        Budget budget = new(Scenario.UserMappings[Username], "Groceries", [TagId], 400m, January,
            RolloverMode.CarryUnderspend);
        budget.ChangeLimit(450.50m, March);
        await SaveAsync(budget);

        // Act
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Budget loaded = await context.Budgets.SingleAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(loaded.Name).IsEqualTo("Groceries");
            await Assert.That(loaded.TagIds).IsEquivalentTo([TagId]);
            await Assert.That(loaded.RolloverMode).IsEqualTo(RolloverMode.CarryUnderspend);
            await Assert.That(loaded.FirstMonth).IsEqualTo(January);
            await Assert.That(loaded.LimitFor(new BudgetMonth(2026, 2))).IsEqualTo(400m);
            await Assert.That(loaded.LimitFor(March)).IsEqualTo(450.50m);
        }
    }

    [Test]
    public async Task Budget_NameAndLimits_AreStoredAsCiphertext()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        await SaveAsync(new Budget(Scenario.UserMappings[Username], "Secret Budget", [TagId], 412.34m, March,
            RolloverMode.None));

        // Act
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        string name = await context.Database
            .SqlQueryRaw<string>("SELECT \"Name\" AS \"Value\" FROM \"Budgets\"")
            .SingleAsync(CancellationToken);
        string limits = await context.Database
            .SqlQueryRaw<string>("SELECT \"Limits\" AS \"Value\" FROM \"Budgets\"")
            .SingleAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(name).DoesNotContain("Secret");
            await Assert.That(limits).DoesNotContain("412.34");
            await Assert.That(limits).DoesNotContain("2026-03");
        }
    }

    [Test]
    public async Task Budget_ChangedLimit_IsSaved()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        await SaveAsync(new Budget(Scenario.UserMappings[Username], "Groceries", [TagId], 400m, January,
            RolloverMode.None));

        // Act
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            Budget tracked = await context.Budgets.SingleAsync(CancellationToken);
            tracked.ChangeLimit(500m, March);
            tracked.ReplaceTags([TagId, Guid.CreateVersion7()]);
            await context.SaveChangesAsync(CancellationToken);
        }

        // Assert
        await using FinanceTrackerContext verificationContext =
            await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Budget loaded = await verificationContext.Budgets.SingleAsync(CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(loaded.LimitFor(January)).IsEqualTo(400m);
            await Assert.That(loaded.LimitFor(March)).IsEqualTo(500m);
            await Assert.That(loaded.TagIds).Count().IsEqualTo(2);
        }
    }

    private async Task SaveAsync(Budget budget)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        context.Budgets.Add(budget);
        await context.SaveChangesAsync(CancellationToken);
    }
}
