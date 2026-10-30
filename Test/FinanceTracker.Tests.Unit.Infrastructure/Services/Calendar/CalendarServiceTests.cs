using FinanceTracker.Contracts.Calendar;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Domain.SavingsGoals;
using FinanceTracker.Infrastructure.Calendar;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Calendar;

public class CalendarServiceTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";

    [Test]
    [Arguments(2, 2026, 28)]
    [Arguments(2, 2028, 29)]
    [Arguments(4, 2026, 30)]
    [Arguments(5, 2026, 31)]
    public async Task GetMonthItemsAsync_ReturnsOneEntryPerDayOfMonth(int month, int year, int expectedDays)
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        CalendarService subject = BuildSubject();

        // Act
        List<CalendarItems> days = await subject.GetMonthItemsAsync(month, year, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(days.Count).IsEqualTo(expectedDays);
            await Assert.That(days.First().Date).IsEqualTo(new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc));
            await Assert.That(days.Last().Date).IsEqualTo(new DateTime(year, month, expectedDays, 0, 0, 0, DateTimeKind.Utc));
        }
    }

    [Test]
    public async Task GetMonthItemsAsync_PlacesTransactionsOnTheirDayAndMapsFields()
    {
        // Arrange
        DateTime occurredAt = new(2026, 5, 15, 12, 30, 0, DateTimeKind.Utc);
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.TransactionScenario.WithTransaction("coffee", "main", new TransactionDetailsFaker().Generate() with
            {
                Description = "Coffee", Amount = -3.5m, Type = TransactionType.Debit, OccurredAt = occurredAt
            }));

        CalendarService subject = BuildSubject();

        // Act
        List<CalendarItems> days = await subject.GetMonthItemsAsync(5, 2026, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        CalendarTransactionItem transaction = days.Single(x => x.Date.Day == 15).Transactions.Single();

        using (Assert.Multiple())
        {
            await Assert.That(days.Where(x => x.Date.Day != 15).All(x => !x.Transactions.Any())).IsTrue();
            await Assert.That(transaction.Description).IsEqualTo("Coffee");
            await Assert.That(transaction.Amount).IsEqualTo(-3.5m);
            await Assert.That(transaction.TransactionType).IsEqualTo("Debit");
            await Assert.That(transaction.TransactionTime).IsEqualTo(occurredAt);
        }
    }

    [Test]
    public async Task GetMonthItemsAsync_LeavesOutTransfersButKeepsThoseUpForReview()
    {
        // Arrange
        DateTime occurredAt = new(2026, 5, 15, 12, 30, 0, DateTimeKind.Utc);
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.TransactionScenario.WithTransaction("to-savings", "main",
                new TransactionDetailsFaker().Generate() with { Description = "To savings", OccurredAt = occurredAt }))
            .Do(s => s.TransactionScenario.WithTransfer("to-savings"))
            .Do(s => s.TransactionScenario.WithTransaction("to-friend", "main",
                new TransactionDetailsFaker().Generate() with { Description = "To friend", OccurredAt = occurredAt }))
            .Do(s => s.TransactionScenario.WithTransfer("to-friend", TransferDecision.NeedsReview));

        CalendarService subject = BuildSubject();

        // Act
        List<CalendarItems> days = await subject.GetMonthItemsAsync(5, 2026, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        await Assert.That(days.SelectMany(static x => x.Transactions).Select(static x => x.Description))
            .IsEquivalentTo(["To friend"]);
    }

    [Test]
    public async Task GetMonthItemsAsync_ExcludesTransactionsOutsideTheMonth()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.TransactionScenario.WithTransaction("before", "main", new TransactionDetailsFaker().Generate() with
            {
                OccurredAt = new DateTime(2026, 4, 30, 23, 59, 59, DateTimeKind.Utc)
            }))
            .Do(s => s.TransactionScenario.WithTransaction("first", "main", new TransactionDetailsFaker().Generate() with
            {
                OccurredAt = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)
            }))
            .Do(s => s.TransactionScenario.WithTransaction("after", "main", new TransactionDetailsFaker().Generate() with
            {
                OccurredAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)
            }));

        CalendarService subject = BuildSubject();

        // Act
        List<CalendarItems> days = await subject.GetMonthItemsAsync(5, 2026, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(days.Sum(x => x.Transactions.Count())).IsEqualTo(1);
            await Assert.That(days.First().Transactions.Count()).IsEqualTo(1);
        }
    }

    [Test]
    public async Task GetMonthItemsAsync_PlacesGoalsOnTheirCompletionDate()
    {
        // Arrange
        DateTime goalDate = new(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc);
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.SavingsGoalScenario.WithSavingsGoal("holiday", Username, userId =>
                new SavingsGoal(userId, "Holiday", 0, 0, 1000, goalDate)))
            .Do(s => s.SavingsGoalScenario.WithSavingsGoal("no-goal", Username, userId =>
                new SavingsGoal(userId, "No Goal", 0, 0, 0, null)))
            .Do(s => s.SavingsGoalScenario.WithSavingsGoal("next-month", Username, userId =>
                new SavingsGoal(userId, "Next Month", 0, 0, 0, goalDate.AddMonths(1))));

        CalendarService subject = BuildSubject();

        // Act
        List<CalendarItems> days = await subject.GetMonthItemsAsync(5, 2026, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        CalendarGoalItem goal = days.Single(x => x.Date.Day == 20).Goals.Single();

        using (Assert.Multiple())
        {
            await Assert.That(days.Sum(x => x.Goals.Count())).IsEqualTo(1);
            await Assert.That(goal.Name).IsEqualTo("Holiday");
            await Assert.That(goal.GoalCompletionDate).IsEqualTo(goalDate);
        }
    }

    [Test]
    public async Task GetMonthItemsAsync_OnlyIncludesCurrentUsersData()
    {
        // Arrange
        DateTime date = new(2026, 5, 10, 9, 0, 0, DateTimeKind.Utc);
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("other-bank", OtherUsername))
            .Do(s => s.BankAccountScenario.WithAccount("other", "other-bank"))
            .Do(s => s.TransactionScenario.WithTransaction("theirs", "other",
                new TransactionDetailsFaker().Generate() with { OccurredAt = date }))
            .Do(s => s.SavingsGoalScenario.WithSavingsGoal("their-goal", OtherUsername, userId =>
                new SavingsGoal(userId, "Theirs", 0, 0, 0, date)));

        CalendarService subject = BuildSubject();

        // Act
        List<CalendarItems> days = await subject.GetMonthItemsAsync(5, 2026, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(days.Sum(x => x.Transactions.Count())).IsEqualTo(0);
            await Assert.That(days.Sum(x => x.Goals.Count())).IsEqualTo(0);
        }
    }

    [Test]
    public async Task GetMonthItemsAsync_ExcludesGoalsBeforeTheMonth()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.SavingsGoalScenario.WithSavingsGoal("last-month", Username, userId =>
                new SavingsGoal(userId, "Last Month", 0, 0, 0, new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc))));

        CalendarService subject = BuildSubject();

        // Act
        List<CalendarItems> days = await subject.GetMonthItemsAsync(5, 2026, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        await Assert.That(days.Sum(x => x.Goals.Count())).IsEqualTo(0);
    }

    private CalendarService BuildSubject() =>
        new(CurrentUserFor(Username), DbContextFactory, NullLogger<CalendarService>.Instance);
}
