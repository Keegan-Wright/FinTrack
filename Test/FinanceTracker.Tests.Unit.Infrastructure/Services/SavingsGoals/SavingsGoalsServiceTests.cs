using FinanceTracker.Contracts.SavingsGoals;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.SavingsGoals;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using DomainSavingsGoal = FinanceTracker.Domain.SavingsGoals.SavingsGoal;
using SavingsGoal = FinanceTracker.Contracts.SavingsGoals.SavingsGoal;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.SavingsGoals;

public class SavingsGoalsServiceTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";

    [Test]
    public async Task AddSavingsGoalAsync_ValidRequest_PersistsForCurrentUserAndReturnsMapped()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        SavingsGoalsService subject = BuildSubject(Username);
        DateTime goalDate = DateTime.UtcNow.Date.AddMonths(3);

        // Act
        SavingsGoal added = await subject.AddSavingsGoalAsync(new AddSavingsGoal
        {
            Name = "Emergency",
            Saved = 100,
            MonthlyContribution = 10,
            Target = 1000,
            TargetDate = goalDate
        }, CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        DomainSavingsGoal persisted = await context.SavingsGoals.SingleAsync(CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(added.Name).IsEqualTo("Emergency");
            await Assert.That(added.Saved).IsEqualTo(100m);
            await Assert.That(added.MonthlyContribution).IsEqualTo(10m);
            await Assert.That(added.Target).IsEqualTo(1000m);
            await Assert.That(added.TargetDate).IsEqualTo(goalDate);
            await Assert.That(persisted.UserId).IsEqualTo(Scenario.UserMappings[Username]);
            await Assert.That(persisted.Name).IsEqualTo("Emergency");
        }
    }

    [Test]
    public async Task AddSavingsGoalAsync_InvalidName_ThrowsAndPersistsNothing()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        SavingsGoalsService subject = BuildSubject(Username);

        // Act
        await Assert.ThrowsAsync<ArgumentException>(() => subject.AddSavingsGoalAsync(new AddSavingsGoal
        {
            Name = " ",
            Saved = 0,
            MonthlyContribution = 0,
            Target = 0,
            TargetDate = null
        }, CancellationToken));

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        await Assert.That(await context.SavingsGoals.CountAsync(CancellationToken)).IsEqualTo(0);
    }

    [Test]
    public async Task GetSavingsGoalsAsync_ReturnsOnlyCurrentUsersGoals()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.SavingsGoalScenario.WithSavingsGoal("mine", Username, userId =>
                new DomainSavingsGoal(userId, "Mine", 1, 2, 3, null)))
            .Do(s => s.SavingsGoalScenario.WithSavingsGoal("theirs", OtherUsername, userId =>
                new DomainSavingsGoal(userId, "Theirs", 1, 2, 3, null)));

        SavingsGoalsService subject = BuildSubject(Username);

        // Act
        List<SavingsGoal> items = await subject.GetSavingsGoalsAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert
        await Assert.That(items.Select(x => x.Name)).IsEquivalentTo(["Mine"]);
    }

    [Test]
    public async Task GetSavingsGoalsAsync_MapsAllFields()
    {
        // Arrange
        DateTime goalDate = new(2027, 1, 31, 0, 0, 0, DateTimeKind.Utc);
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.SavingsGoalScenario.WithSavingsGoal("holiday", Username, userId =>
                new DomainSavingsGoal(userId, "Holiday", 250.50m, 75m, 2000m, goalDate)));

        SavingsGoalsService subject = BuildSubject(Username);

        // Act
        SavingsGoal item = (await subject.GetSavingsGoalsAsync(CancellationToken).ToListAsync(CancellationToken)).Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(item.Name).IsEqualTo("Holiday");
            await Assert.That(item.Saved).IsEqualTo(250.50m);
            await Assert.That(item.MonthlyContribution).IsEqualTo(75m);
            await Assert.That(item.Target).IsEqualTo(2000m);
            await Assert.That(item.TargetDate).IsEqualTo(goalDate);
        }
    }

    [Test]
    public async Task GetSavingsGoalsAsync_WhenUserHasNoSavingsGoals_ReturnsEmpty()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        SavingsGoalsService subject = BuildSubject(Username);

        // Act
        List<SavingsGoal> items = await subject.GetSavingsGoalsAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert
        await Assert.That(items.Count).IsEqualTo(0);
    }

    [Test]
    public async Task DeleteSavingsGoalAsync_WhenExists_RemovesGoalAndReturnsTrue()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.SavingsGoalScenario.WithSavingsGoal("disposable", Username))
            .Do(s => s.SavingsGoalScenario.WithSavingsGoal("kept", Username));

        SavingsGoalsService subject = BuildSubject(Username);

        // Act
        bool deleted = await subject.DeleteSavingsGoalAsync(Scenario.SavingsGoalMappings["disposable"],
            CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        List<Guid> remaining = await context.SavingsGoals.Select(x => x.Id).ToListAsync(CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(deleted).IsTrue();
            await Assert.That(remaining).IsEquivalentTo([Scenario.SavingsGoalMappings["kept"]]);
        }
    }

    [Test]
    public async Task DeleteSavingsGoalAsync_WhenNotFound_ReturnsFalse()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        SavingsGoalsService subject = BuildSubject(Username);

        // Act
        bool deleted = await subject.DeleteSavingsGoalAsync(Guid.NewGuid(), CancellationToken);

        // Assert
        await Assert.That(deleted).IsFalse();
    }

    [Test]
    public async Task DeleteSavingsGoalAsync_WhenOwnedByAnotherUser_ReturnsFalseAndKeepsGoal()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.SavingsGoalScenario.WithSavingsGoal("theirs", OtherUsername));

        SavingsGoalsService subject = BuildSubject(Username);

        // Act
        bool deleted = await subject.DeleteSavingsGoalAsync(Scenario.SavingsGoalMappings["theirs"],
            CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(deleted).IsFalse();
            await Assert.That(await context.SavingsGoals.CountAsync(CancellationToken)).IsEqualTo(1);
        }
    }

    private SavingsGoalsService BuildSubject(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<SavingsGoalsService>.Instance);
}
