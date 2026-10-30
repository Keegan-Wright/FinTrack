using Bunit;
using FinanceTracker.Client.Components.Dashboard;
using FinanceTracker.Contracts.Budgets;

namespace FinanceTracker.Tests.Unit.Client.Dashboard;

public class BudgetsCardTests : ComponentTestBase
{
    [Test]
    public async Task Nearest_PutsTheMostUsedUpFirst_UpToFive()
    {
        // Arrange
        BudgetMonthOverview overview = new()
        {
            IsCurrent = true,
            Budgets = Enumerable.Range(1, 7).Select(static x => Status($"Budget {x}", spent: x * 10m)).ToList()
        };

        // Act
        IRenderedComponent<BudgetsCard> cut = Render<BudgetsCard>(p => p.Add(x => x.Overview, overview));

        // Assert
        await Assert.That(cut.Instance.Nearest.Select(static x => x.Name)).IsEquivalentTo(
            ["Budget 7", "Budget 6", "Budget 5", "Budget 4", "Budget 3"],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task BudgetWithoutTags_ComesFirstAndIsCalledOut()
    {
        // Arrange
        BudgetStatus untagged = Status("Untagged", spent: 0m);
        untagged = new BudgetStatus
        {
            BudgetId = untagged.BudgetId,
            Name = untagged.Name,
            CoversNothing = true,
            Limit = 100m,
            Available = 100m,
            Remaining = 100m
        };
        BudgetMonthOverview overview = new()
        {
            IsCurrent = true,
            Budgets = [Status("Busy", spent: 90m), untagged]
        };

        // Act
        IRenderedComponent<BudgetsCard> cut = Render<BudgetsCard>(p => p.Add(x => x.Overview, overview));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Instance.Nearest[0].Name).IsEqualTo("Untagged");
            await Assert.That(cut.Markup).Contains("1 budget needs a tag to track spending");
            await Assert.That(cut.Markup).Contains("Its tags were deleted");
        }
    }

    [Test]
    public async Task NoBudgets_SaysHowToSetOneUp()
    {
        // Act
        IRenderedComponent<BudgetsCard> cut = Render<BudgetsCard>(p => p.Add(x => x.Overview,
            new BudgetMonthOverview { IsCurrent = true }));

        // Assert
        await Assert.That(cut.Markup).Contains("No budgets yet");
    }

    private static BudgetStatus Status(string name, decimal spent) => new()
    {
        BudgetId = Guid.NewGuid(),
        Name = name,
        Limit = 100m,
        Available = 100m,
        Spent = spent,
        Remaining = 100m - spent
    };
}
