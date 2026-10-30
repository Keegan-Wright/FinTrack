using FinanceTracker.Domain.SavingsGoals;

namespace FinanceTracker.Tests.Unit.Domain.SavingsGoals;

[Category("Domain")]
public class SavingsGoalTests
{

    [Test]
    [Arguments("", 100, 100, 1000)]
    [Arguments("Test", -1, 100, 1000)]
    [Arguments("Test", 100, -1, 1000)]
    [Arguments("Test", 100, 100, -1)]
    public void SavingsGoal_InvalidConstruction_Throws_ArgumentException(string name, decimal saved, decimal monthlyContribution, decimal target)
    {
        Assert.Throws<ArgumentException>(() =>
            _ = new SavingsGoal(Guid.NewGuid(), name, saved, monthlyContribution, target, null));
    }

    [Test]
    public void SavingsGoal_Rename_EmptyString_Throws_ArgumentException()
    {
        var goal = new SavingsGoal(Guid.NewGuid(), "Test", 0m, 0m, 0m, null);

        Assert.Throws<ArgumentException>(() =>
            goal.Rename(""));
    }

    [Test]
    public async Task SavingsGoal_Rename_ValidString_DoesNotThrow()
    {
        var goal = new SavingsGoal(Guid.NewGuid(), "Test", 0m, 0m, 0m, null);
        string newName = "Testing Category";

        goal.Rename(newName);

        await Assert.That(goal.Name).IsEqualTo(newName);
    }

    [Test]
    public void SavingsGoal_ChangeMonthlyContribution_Negative_Throws_ArgumentOutOfRangeException()
    {
        var goal = new SavingsGoal(Guid.NewGuid(), "Test", 0m, 0m, 0m, null);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            goal.ChangeMonthlyContribution(-1));
    }


    [Test]
    public async Task SavingsGoal_ChangeMonthlyContribution_ValidDecimal_DoesNotThrow()
    {
        var goal = new SavingsGoal(Guid.NewGuid(), "Test", 0m, 0m, 0m, null);
        decimal newMonthlyContribution =  200m;

        goal.ChangeMonthlyContribution(newMonthlyContribution);

        await Assert.That(goal.MonthlyContribution).IsEqualTo(newMonthlyContribution);
    }


    [Test]
    public void SavingsGoal_SetTarget_Negative_Throws_ArgumentOutOfRangeException()
    {
        var goal = new SavingsGoal(Guid.NewGuid(), "Test", 0m, 0m, 0m, null);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            goal.SetTarget(-1, DateTime.UtcNow));
    }

    [Test]
    public async Task SavingsGoal_SetTarget_ValidDecimalWithDate_DoesNotThrow()
    {
        var goal = new SavingsGoal(Guid.NewGuid(), "Test", 0m, 0m, 0m, null);

        decimal newSavingGoalValue = 200m;
        DateTime newTargetDate = DateTime.UtcNow;

        goal.SetTarget(newSavingGoalValue, newTargetDate);

        using (Assert.Multiple())
        {
            await Assert.That(goal.Target).IsEqualTo(newSavingGoalValue);
            await Assert.That(goal.TargetDate).IsEqualTo(newTargetDate);
        }
    }


    [Test]
    public async Task SavingsGoal_SetTarget_ValidDecimalNoDate_DoesNotThrow()
    {
        var goal = new SavingsGoal(Guid.NewGuid(), "Test", 0m, 0m, 0m, DateTime.UtcNow);

        decimal newSavingGoalValue = 200m;

        goal.SetTarget(newSavingGoalValue, null);

        using (Assert.Multiple())
        {
            await Assert.That(goal.Target).IsEqualTo(newSavingGoalValue);
            await Assert.That(goal.TargetDate).IsNull();
        }
    }


    [Test]
    public void SavingsGoal_AddSavings_Negative_Throws_ArgumentOutOfRangeException()
    {
        var goal = new SavingsGoal(Guid.NewGuid(), "Test", 0m, 0m, 0m, null);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            goal.AddSavings(-1));
    }

    [Test]
    public async Task SavingsGoal_AddSavings_ValidDecimalNoDate_DoesNotThrow()
    {
        var goal = new SavingsGoal(Guid.NewGuid(), "Test", 0m, 0m, 0m, DateTime.UtcNow);

        decimal newSaved = 200m;

        goal.AddSavings(newSaved);


       await Assert.That(goal.Saved).IsEqualTo(newSaved);
    }

    [Test]
    public async Task SavingsGoal_AddSavings_AddsToExistingSavings()
    {
        var goal = new SavingsGoal(Guid.NewGuid(), "Test", 50m, 0m, 0m, null);

        goal.AddSavings(25m);
        goal.AddSavings(0m);

        await Assert.That(goal.Saved).IsEqualTo(75m);
    }

    [Test]
    public async Task SavingsGoal_ValidConstruction_SetsProperties()
    {
        var userId = Guid.NewGuid();
        var goalDate = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var goal = new SavingsGoal(userId, "Holiday", 100m, 25m, 1000m, goalDate);

        using (Assert.Multiple())
        {
            await Assert.That(goal.UserId).IsEqualTo(userId);
            await Assert.That(goal.Name).IsEqualTo("Holiday");
            await Assert.That(goal.Saved).IsEqualTo(100m);
            await Assert.That(goal.MonthlyContribution).IsEqualTo(25m);
            await Assert.That(goal.Target).IsEqualTo(1000m);
            await Assert.That(goal.TargetDate).IsEqualTo(goalDate);
        }
    }

    [Test]
    public async Task SavingsGoal_ZeroValues_AreValid()
    {
        var goal = new SavingsGoal(Guid.NewGuid(), "Test", 0m, 0m, 0m, null);

        using (Assert.Multiple())
        {
            await Assert.That(goal.Saved).IsEqualTo(0m);
            await Assert.That(goal.MonthlyContribution).IsEqualTo(0m);
            await Assert.That(goal.Target).IsEqualTo(0m);
        }
    }

    [Test]
    [Arguments(null)]
    [Arguments(" ")]
    public void SavingsGoal_Rename_NullOrWhitespace_Throws_ArgumentException(string? name)
    {
        var goal = new SavingsGoal(Guid.NewGuid(), "Test", 0m, 0m, 0m, null);

        Assert.Throws<ArgumentException>(() =>
            goal.Rename(name!));
    }

    [Test]
    public void SavingsGoal_EmptyUserId_Throws_ArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _ = new SavingsGoal(Guid.Empty, "Test", 0m, 0m, 0m, null));
    }

    [Test]
    public async Task SavingsGoal_TrackTags_SwitchesSourceAndKeepsTheStart()
    {
        var goal = new SavingsGoal(Guid.NewGuid(), "Car", 0m, 0m, 0m, null);
        Guid tagId = Guid.NewGuid();

        goal.TrackTags([tagId, tagId], 150m, new DateOnly(2026, 2, 1));

        using (Assert.Multiple())
        {
            await Assert.That(goal.Source).IsEqualTo(SavingsGoalSource.Tags);
            await Assert.That(goal.TagIds).IsEquivalentTo([tagId]);
            await Assert.That(goal.Saved).IsEqualTo(150m);
            await Assert.That(goal.TrackedFrom).IsEqualTo(new DateOnly(2026, 2, 1));
        }
    }

    [Test]
    public async Task SavingsGoal_TrackAccount_ThenManually_ClearsTheAccount()
    {
        var goal = new SavingsGoal(Guid.NewGuid(), "Car", 0m, 0m, 0m, null);
        goal.TrackAccount(Guid.NewGuid());

        goal.TrackManually(40m);

        using (Assert.Multiple())
        {
            await Assert.That(goal.Source).IsEqualTo(SavingsGoalSource.Manual);
            await Assert.That(goal.AccountId).IsNull();
            await Assert.That(goal.Saved).IsEqualTo(40m);
        }
    }

    [Test]
    public void SavingsGoal_TrackTags_None_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            new SavingsGoal(Guid.NewGuid(), "Car", 0m, 0m, 0m, null).TrackTags([], 0m, new DateOnly(2026, 1, 1)));

    [Test]
    public async Task SavingsGoal_RemoveTag_ItsLast_CoversNothing()
    {
        var goal = new SavingsGoal(Guid.NewGuid(), "Car", 0m, 0m, 0m, null);
        Guid tagId = Guid.NewGuid();
        goal.TrackTags([tagId], 0m, new DateOnly(2026, 1, 1));

        goal.RemoveTag(tagId);

        await Assert.That(goal.CoversNothing).IsTrue();
    }
}
