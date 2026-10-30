using FinanceTracker.Client.Components.SavingsGoals;
using FinanceTracker.Contracts.SavingsGoals;

namespace FinanceTracker.Tests.Unit.Client.SavingsGoals;

public class SavingsGoalTextTests
{
    private static readonly Guid CarFund = Guid.NewGuid();
    private static readonly Dictionary<Guid, string> TagNames = new() { [CarFund] = "Car fund" };

    [Test]
    public async Task Pace_OnTrack_SaysWhenItGetsThere()
    {
        // Arrange
        using CultureScope culture = new();
        SavingsGoal goal = Goal(onTrack: true, reachedBy: new DateOnly(2027, 6, 1));

        // Act & Assert
        await Assert.That(SavingsGoalText.Pace(goal)).IsEqualTo("On track, £250.00 a month reaches it by Jun 2027");
    }

    [Test]
    public async Task Pace_BehindTheDate_SaysWhatsNeeded()
    {
        // Arrange
        using CultureScope culture = new();
        SavingsGoal goal = Goal(onTrack: false, reachedBy: new DateOnly(2028, 1, 1), needed: 320m);

        // Act & Assert
        await Assert.That(SavingsGoalText.Pace(goal)).IsEqualTo("Needs £320.00 a month to make Jun 2027");
    }

    [Test]
    public async Task Pace_Reached_SaysSo() =>
        await Assert.That(SavingsGoalText.Pace(Goal(saved: 10000m))).IsEqualTo("Reached");

    [Test]
    public async Task Source_SaysWhereTheAmountComesFrom()
    {
        // Arrange
        using CultureScope culture = new();
        SavingsGoal account = Goal(source: SavingsGoalSource.Account, accountName: "Rainy Day");
        SavingsGoal removed = Goal(source: SavingsGoalSource.Account);
        SavingsGoal tagged = Goal(source: SavingsGoalSource.Tags);

        // Act & Assert
        using (Assert.Multiple())
        {
            await Assert.That(SavingsGoalText.Source(account, TagNames)).IsEqualTo("Balance of Rainy Day");
            await Assert.That(SavingsGoalText.Source(removed, TagNames)).IsEqualTo("Balance of a removed account");
            await Assert.That(SavingsGoalText.Source(tagged, TagNames)).IsEqualTo("Tagged Car fund since 1 Feb 2026");
            await Assert.That(SavingsGoalText.Source(Goal(), TagNames)).IsEqualTo("Typed in");
        }
    }

    [Test]
    public async Task Problem_RemovedAccountOrDeletedTags_SaysWhatToDo()
    {
        using (Assert.Multiple())
        {
            await Assert.That(SavingsGoalText.Problem(Goal(source: SavingsGoalSource.Account)))
                .IsEqualTo("Its account was removed, edit it to choose another");
            await Assert.That(SavingsGoalText.Problem(Goal(source: SavingsGoalSource.Tags, coversNothing: true)))
                .IsEqualTo("Its tags were deleted, edit it to choose one");
            await Assert.That(SavingsGoalText.Problem(Goal())).IsNull();
        }
    }

    private static SavingsGoal Goal(decimal saved = 1000m, bool? onTrack = null, DateOnly? reachedBy = null,
        decimal? needed = null, SavingsGoalSource source = SavingsGoalSource.Manual, string? accountName = null,
        bool coversNothing = false) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Car",
        Saved = saved,
        Target = 10000m,
        TargetDate = new DateTime(2027, 6, 1),
        MonthlyContribution = 250m,
        Source = source,
        AccountName = accountName,
        TagIds = [CarFund],
        TrackedFrom = new DateOnly(2026, 2, 1),
        CoversNothing = coversNothing,
        OnTrack = onTrack,
        ReachedBy = reachedBy,
        NeededPerMonth = needed
    };
}
