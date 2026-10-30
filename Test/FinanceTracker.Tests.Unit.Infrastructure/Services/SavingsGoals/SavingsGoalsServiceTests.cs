using FinanceTracker.Contracts.SavingsGoals;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.SavingsGoals;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Fakers;
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
        SavingsGoal added = await subject.AddSavingsGoalAsync(new SaveSavingsGoal
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
        await Assert.ThrowsAsync<ArgumentException>(() => subject.AddSavingsGoalAsync(new SaveSavingsGoal
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
        SavingsGoal item = (await subject.GetSavingsGoalsAsync(CancellationToken).ToListAsync(CancellationToken))
            .Single();

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

    [Test]
    public async Task AddSavingsGoalAsync_BackedByAnAccount_SavedIsItsBalance()
    {
        // Arrange
        await SeedAccountsAsync(Username, balance: 1234.56m);

        // Act
        SavingsGoal added = await BuildSubject(Username).AddSavingsGoalAsync(Backed("savings"), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(added.Source).IsEqualTo(SavingsGoalSource.Account);
            await Assert.That(added.Saved).IsEqualTo(1234.56m);
            await Assert.That(added.AccountName).IsEqualTo("Rainy Day");
        }
    }

    [Test]
    public async Task AddSavingsGoalAsync_AccountAlreadyBacksAGoal_Throws()
    {
        // Arrange
        await SeedAccountsAsync(Username);
        SavingsGoalsService subject = BuildSubject(Username);
        await subject.AddSavingsGoalAsync(Backed("savings"), CancellationToken);

        // Act & Assert
        await Assert.That(async () => await subject.AddSavingsGoalAsync(Backed("savings"), CancellationToken))
            .Throws<InvalidOperationException>();
    }

    [Test]
    [Arguments("card")]
    [Arguments("theirs")]
    public async Task AddSavingsGoalAsync_CardOrAnotherUsersAccount_Throws(string accountName)
    {
        // Arrange
        await SeedAccountsAsync(Username);
        await Scenario.Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("their-bank", OtherUsername))
            .Do(s => s.BankAccountScenario.WithAccount("theirs", "their-bank"));

        // Act & Assert
        await Assert.That(async () => await BuildSubject(Username)
                .AddSavingsGoalAsync(Backed(accountName), CancellationToken))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task GetSavingsGoalsAsync_TrackingTags_CountsWhatsTaggedSinceItsDay()
    {
        // Arrange
        await SeedAccountsAsync(Username)
            .Do(s => s.ClassificationScenario.WithClassification("car", Username, "Car fund"))
            .Do(s => s.TransactionScenario.WithTransaction("paid-in", "savings", Moved(300m, new DateTime(2026, 3, 2))))
            .Do(s => s.TransactionScenario.WithCustomClassification("paid-in", "car"))
            .Do(s => s.TransactionScenario.WithTransaction("before", "savings", Moved(999m, new DateTime(2026, 1, 2))))
            .Do(s => s.TransactionScenario.WithCustomClassification("before", "car"));
        SavingsGoalsService subject = BuildSubject(Username);
        await subject.AddSavingsGoalAsync(new SaveSavingsGoal
        {
            Name = "Car",
            Target = 10000m,
            Source = SavingsGoalSource.Tags,
            Saved = 1000m,
            TagIds = [Scenario.ClassificationMappings["car"]],
            TrackedFrom = new DateOnly(2026, 2, 1)
        }, CancellationToken);

        // Act
        SavingsGoal goal = (await subject.GetSavingsGoalsAsync(CancellationToken).ToListAsync(CancellationToken))
            .Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(goal.Saved).IsEqualTo(1300m);
            await Assert.That(goal.StartingAmount).IsEqualTo(1000m);
        }
    }

    [Test]
    public async Task UpdateSavingsGoalAsync_FromAnAccountToTypedIn_FreesTheAccount()
    {
        // Arrange
        await SeedAccountsAsync(Username);
        SavingsGoalsService subject = BuildSubject(Username);
        SavingsGoal goal = await subject.AddSavingsGoalAsync(Backed("savings"), CancellationToken);

        // Act
        SavingsGoal updated = await subject.UpdateSavingsGoalAsync(goal.Id,
            new SaveSavingsGoal { Name = "Car", Source = SavingsGoalSource.Manual, Saved = 50m }, CancellationToken);
        List<SavingsGoalAccount> accounts = await subject.GetSavingsAccountsAsync(CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(updated.Saved).IsEqualTo(50m);
            await Assert.That(updated.AccountId).IsNull();
            await Assert.That(accounts.All(static x => x.GoalId is null)).IsTrue();
        }
    }

    [Test]
    public async Task GetSavingsAccountsAsync_LeavesOutCards_AndSaysWhatEachBacks()
    {
        // Arrange
        await SeedAccountsAsync(Username);
        SavingsGoalsService subject = BuildSubject(Username);
        SavingsGoal goal = await subject.AddSavingsGoalAsync(Backed("savings"), CancellationToken);

        // Act
        List<SavingsGoalAccount> accounts = await subject.GetSavingsAccountsAsync(CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(accounts.Select(static x => x.Name)).IsEquivalentTo(["Current", "Rainy Day"]);
            await Assert.That(accounts.Single(static x => x.Name == "Rainy Day").GoalId).IsEqualTo(goal.Id);
        }
    }

    private Task<TestingScenario> SeedAccountsAsync(string username, decimal balance = 0m) =>
        Scenario.Do(s => s.UserScenario.WithUser(username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", username))
            .Do(s => s.BankAccountScenario.WithAccount("current", "bank", (userId, connectionId) =>
                new BankAccount(userId, connectionId, "acc-current", "TRANSACTION", "Current", "GBP")))
            .Do(s => s.BankAccountScenario.WithAccount("savings", "bank", (userId, connectionId) =>
            {
                BankAccount account = new(userId, connectionId, "acc-savings", "SAVINGS", "Rainy Day", "GBP");
                account.UpdateBalance(balance, balance, DateTime.UtcNow);
                return account;
            }))
            .Do(s => s.BankAccountScenario.WithAccount("card", "bank", (userId, connectionId) =>
                new BankAccount(userId, connectionId, "acc-card", "CREDIT_CARD", "Card", "GBP",
                    BankAccountKind.Card)));

    private SaveSavingsGoal Backed(string accountName) => new()
    {
        Name = "Car",
        Target = 10000m,
        Source = SavingsGoalSource.Account,
        AccountId = Scenario.AccountMappings[accountName]
    };

    private static TransactionDetails Moved(decimal amount, DateTime occurredAt) =>
        new TransactionDetailsFaker().Generate() with
        {
            Amount = amount,
            Type = amount < 0 ? TransactionType.Debit : TransactionType.Credit,
            OccurredAt = DateTime.SpecifyKind(occurredAt, DateTimeKind.Utc),
            IsPending = false
        };

    private SavingsGoalsService BuildSubject(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<SavingsGoalsService>.Instance,
            new FixedTimeProvider(new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc)));
}
