using Bunit;
using FinanceTracker.Client.Components.Dashboard;
using FinanceTracker.Contracts.SavingsGoals;

namespace FinanceTracker.Tests.Unit.Client.Dashboard;

public class SavingsGoalsCardTests : ComponentTestBase
{
    [Test]
    public async Task Nearest_PutsTheClosestToTheirTargetFirst_UpToFive()
    {
        // Arrange
        List<SavingsGoal> goals = Enumerable.Range(1, 7).Select(static x => Goal($"Goal {x}", saved: x * 10m)).ToList();

        // Act
        IRenderedComponent<SavingsGoalsCard> cut = Render<SavingsGoalsCard>(p => p.Add(x => x.SavingsGoals, goals));

        // Assert
        await Assert.That(cut.Instance.Nearest.Select(static x => x.Name)).IsEquivalentTo(
            ["Goal 7", "Goal 6", "Goal 5", "Goal 4", "Goal 3"],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    private static SavingsGoal Goal(string name, decimal saved) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Saved = saved,
        Target = 100m
    };
}
