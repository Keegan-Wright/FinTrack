using FinanceTracker.Client.Components.SavingsGoals;
using FinanceTracker.Contracts.SavingsGoals;

namespace FinanceTracker.Tests.Unit.Client.SavingsGoals;

public class SavingsDirectionTextTests
{
    [Test]
    [Arguments(SavingsDirection.MoneyIn, "Money in with any of these adds to it, money out takes from it")]
    [Arguments(SavingsDirection.MoneyOut, "Money out with any of these adds to it, money back in takes from it")]
    public async Task TagsHelperText_SaysWhichWayTheTagsCount(SavingsDirection direction, string expected) =>
        await Assert.That(SavingsGoalDialog.TagsHelperText(direction)).IsEqualTo(expected);

    [Test]
    [Arguments(SavingsDirection.MoneyIn, "Tagged Car fund since 1 Feb 2026")]
    [Arguments(SavingsDirection.MoneyOut, "Tagged Car fund going out since 1 Feb 2026")]
    public async Task Source_TagGoal_SaysWhichWayItCounts(SavingsDirection direction, string expected)
    {
        using CultureScope culture = new();
        Guid tagId = Guid.NewGuid();
        SavingsGoal goal = new()
        {
            Id = Guid.NewGuid(),
            Name = "Car",
            Source = SavingsGoalSource.Tags,
            TagIds = [tagId],
            TrackedFrom = new DateOnly(2026, 2, 1),
            Direction = direction
        };

        await Assert.That(SavingsGoalText.Source(goal, new Dictionary<Guid, string> { [tagId] = "Car fund" }))
            .IsEqualTo(expected);
    }

    [Test]
    [Arguments(SavingsDirection.MoneyIn, "Into an account here")]
    [Arguments(SavingsDirection.MoneyOut, "Somewhere FinTrack can't see")]
    public async Task DirectionName_SaysWhereTheMoneyIsSaved(SavingsDirection direction, string expected) =>
        await Assert.That(SavingsGoalText.DirectionName(direction)).IsEqualTo(expected);
}
