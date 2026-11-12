using FinanceTracker.Contracts;
using FinanceTracker.Contracts.Calendar;
using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.Debts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Domain.SavingsGoals;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Calendar;
using FinanceTracker.Infrastructure.Debts;
using FinanceTracker.Infrastructure.SavingsGoals;
using FinanceTracker.Infrastructure.Sharing;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Assertions.Enums;
using BillState = FinanceTracker.Contracts.Bills.BillState;
using CalendarEvent = FinanceTracker.Contracts.Calendar.CalendarEvent;
using DomainCalendarEvent = FinanceTracker.Domain.Calendar.CalendarEvent;
using DomainEventRecurrence = FinanceTracker.Domain.Calendar.EventRecurrence;
using EventRecurrence = FinanceTracker.Contracts.Calendar.EventRecurrence;

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
    public async Task GetMonthItemsAsync_PlacesBillsOnTheirDueDatesWithTheirState()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.BillScenario.WithBill("streaming", Username, "main", static (userId, accountId) =>
                new Bill(userId, accountId, "Streaming", "Streamco", BillFrequency.Weekly, 5m,
                    new DateOnly(2026, 5, 4), BillSource.Manual)));

        CalendarService subject = BuildSubject();

        // Act
        List<CalendarItems> days = await subject.GetMonthItemsAsync(5, 2026, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert, today is the 18th so the earlier ones are past their grace and late
        using (Assert.Multiple())
        {
            await Assert.That(days.Where(x => x.Bills.Any()).Select(x => x.Date.Day)).IsEquivalentTo([4, 11, 18, 25]);
            await Assert.That(days.Single(x => x.Date.Day == 4).Bills.Single().State).IsEqualTo(BillState.Late);
            await Assert.That(days.Single(x => x.Date.Day == 18).Bills.Single().State)
                .IsEqualTo(BillState.Upcoming);
            await Assert.That(days.Single(x => x.Date.Day == 25).Bills.Single().Amount).IsEqualTo(5m);
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

    [Test]
    public async Task AddEventAsync_Yearly_ShowsOnItsDayEachYearFromThen()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        CalendarService subject = BuildSubject();

        // Act
        CalendarEvent added = await subject.AddEventAsync(Birthday(), CancellationToken);
        List<CalendarItems> march = await subject.GetMonthItemsAsync(3, 2027, CancellationToken)
            .ToListAsync(CancellationToken);
        List<CalendarItems> marchBefore = await subject.GetMonthItemsAsync(3, 2025, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        CalendarEventItem onTheDay = march.Single(x => x.Date.Day == 14).Events.Single();
        using (Assert.Multiple())
        {
            await Assert.That(added.NextOn).IsEqualTo(new DateOnly(2027, 3, 14));
            await Assert.That(march.Sum(x => x.Events.Count())).IsEqualTo(1);
            await Assert.That(onTheDay.EventId).IsEqualTo(added.Id);
            await Assert.That(onTheDay.Title).IsEqualTo("Birthday");
            await Assert.That(onTheDay.IsYearly).IsTrue();
            await Assert.That(marchBefore.Sum(x => x.Events.Count())).IsEqualTo(0);
        }
    }

    [Test]
    public async Task AddEventAsync_CreatingAGoal_AimsOneAtItsNextDate()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        CalendarService subject = BuildSubject();
        SaveCalendarEvent birthday = Birthday();
        birthday.SavingsTarget = 100m;
        birthday.CreateSavingsGoal = true;
        birthday.RollsGoalOn = true;

        // Act
        CalendarEvent added = await subject.AddEventAsync(birthday, CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        SavingsGoal goal = await context.SavingsGoals.SingleAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(added.SavingsGoalId).IsEqualTo(goal.Id);
            await Assert.That(added.SavingsGoalName).IsEqualTo("Birthday");
            await Assert.That(added.RollsGoalOn).IsTrue();
            await Assert.That(goal.Name).IsEqualTo("Birthday");
            await Assert.That(goal.Target).IsEqualTo(100m);
            await Assert.That(goal.TargetDate).IsEqualTo(new DateTime(2027, 3, 14));
        }
    }

    [Test]
    public async Task AddEventAsync_AnotherUsersGoal_Throws()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.SavingsGoalScenario.WithSavingsGoal("theirs", OtherUsername));
        CalendarService subject = BuildSubject();
        SaveCalendarEvent birthday = Birthday();
        birthday.SavingsGoalId = Scenario.SavingsGoalMappings["theirs"];

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            subject.AddEventAsync(birthday, CancellationToken));
    }

    [Test]
    public async Task GetMonthItemsAsync_EventSavingInAGoal_SaysWhatsSaved()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.SavingsGoalScenario.WithSavingsGoal("gifts", Username, userId =>
                new SavingsGoal(userId, "Gifts", 40m, 0m, 100m, null)));
        CalendarService subject = BuildSubject();
        SaveCalendarEvent birthday = Birthday();
        birthday.SavingsTarget = 100m;
        birthday.SavingsGoalId = Scenario.SavingsGoalMappings["gifts"];
        await subject.AddEventAsync(birthday, CancellationToken);

        // Act
        List<CalendarItems> march = await subject.GetMonthItemsAsync(3, 2027, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        CalendarEventItem item = march.Single(x => x.Date.Day == 14).Events.Single();
        using (Assert.Multiple())
        {
            await Assert.That(item.SavingsGoalName).IsEqualTo("Gifts");
            await Assert.That(item.Saved).IsEqualTo(40m);
            await Assert.That(item.SavingsTarget).IsEqualTo(100m);
        }
    }

    [Test]
    public async Task AddEventAsync_CreatingAGoalWithATakenName_ThrowsAndAddsNothing()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.SavingsGoalScenario.WithSavingsGoal("birthday", Username, userId =>
                new SavingsGoal(userId, "Birthday", 0, 0, 50, null)));
        CalendarService subject = BuildSubject();
        SaveCalendarEvent birthday = Birthday();
        birthday.SavingsTarget = 100m;
        birthday.CreateSavingsGoal = true;

        // Act
        await Assert.ThrowsAsync<NameTakenException>(() => subject.AddEventAsync(birthday, CancellationToken));

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(await context.CalendarEvents.CountAsync(CancellationToken)).IsEqualTo(0);
            await Assert.That(await context.SavingsGoals.CountAsync(CancellationToken)).IsEqualTo(1);
        }
    }

    [Test]
    public async Task UpdateEventAsync_WithoutAGoal_UnlinksItAndStopsRollingOn()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        CalendarService subject = BuildSubject();
        SaveCalendarEvent birthday = Birthday();
        birthday.SavingsTarget = 100m;
        birthday.CreateSavingsGoal = true;
        birthday.RollsGoalOn = true;
        CalendarEvent added = await subject.AddEventAsync(birthday, CancellationToken);

        // Act
        CalendarEvent updated = await subject.UpdateEventAsync(added.Id, Birthday("Mum's birthday"),
            CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(updated.Title).IsEqualTo("Mum's birthday");
            await Assert.That(updated.SavingsGoalId).IsNull();
            await Assert.That(updated.RollsGoalOn).IsFalse();
        }
    }

    [Test]
    public async Task DeleteEventAsync_KeepsItsGoal()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        CalendarService subject = BuildSubject();
        SaveCalendarEvent birthday = Birthday();
        birthday.SavingsTarget = 100m;
        birthday.CreateSavingsGoal = true;
        CalendarEvent added = await subject.AddEventAsync(birthday, CancellationToken);

        // Act
        await subject.DeleteEventAsync(added.Id, CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(await context.CalendarEvents.CountAsync(CancellationToken)).IsEqualTo(0);
            await Assert.That(await context.SavingsGoals.CountAsync(CancellationToken)).IsEqualTo(1);
        }
    }

    [Test]
    public async Task UpdateOrDeleteEventAsync_AnotherUsersEvent_Throws()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.CalendarEventScenario.WithEvent("theirs", OtherUsername, userId =>
                new DomainCalendarEvent(userId, "Trip", new DateOnly(2026, 8, 1), DomainEventRecurrence.None)));
        CalendarService subject = BuildSubject();
        Guid theirs = Scenario.CalendarEventMappings["theirs"];

        // Act & Assert
        using (Assert.Multiple())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                subject.UpdateEventAsync(theirs, Birthday(), CancellationToken));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                subject.DeleteEventAsync(theirs, CancellationToken));
        }
    }

    [Test]
    public async Task GetEventsAsync_ByTheNextDateTheyreOn_PassedOneOffsLast()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.CalendarEventScenario.WithEvent("past-trip", Username, userId =>
                new DomainCalendarEvent(userId, "Past trip", new DateOnly(2026, 1, 10), DomainEventRecurrence.None)))
            .Do(s => s.CalendarEventScenario.WithEvent("trip", Username, userId =>
                new DomainCalendarEvent(userId, "Trip", new DateOnly(2026, 8, 1), DomainEventRecurrence.None)))
            .Do(s => s.CalendarEventScenario.WithEvent("birthday", Username, userId =>
                new DomainCalendarEvent(userId, "Birthday", new DateOnly(2020, 6, 2), DomainEventRecurrence.Yearly)))
            .Do(s => s.CalendarEventScenario.WithEvent("anniversary", Username, userId =>
                new DomainCalendarEvent(userId, "Anniversary", new DateOnly(2020, 3, 1), DomainEventRecurrence.Yearly)));
        CalendarService subject = BuildSubject();

        // Act
        List<CalendarEvent> calendarEvents = await subject.GetEventsAsync(CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        await Assert.That(calendarEvents.Select(static x => x.Title))
            .IsEquivalentTo(["Birthday", "Trip", "Anniversary", "Past trip"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task GetMonthItemsAsync_PlacesADebtsForecastPayOffAndGoal()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.DebtScenario.WithDebt("car", Username, userId =>
            {
                Debt debt = new(userId, "Car loan", null, 1000m, new DateOnly(2026, 5, 1), 0m, 250m);
                debt.SetPayOffGoal(new DateOnly(2026, 9, 30));
                return debt;
            }));
        CalendarService subject = BuildSubject();

        // Act
        List<CalendarItems> september = await subject.GetMonthItemsAsync(9, 2026, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        CalendarDebtItem paidOff = september.Single(x => x.Date.Day == 18).Debts.Single();
        CalendarDebtItem goal = september.Single(x => x.Date.Day == 30).Debts.Single();
        using (Assert.Multiple())
        {
            await Assert.That(september.Sum(x => x.Debts.Count())).IsEqualTo(2);
            await Assert.That(paidOff.Name).IsEqualTo("Car loan");
            await Assert.That(paidOff.Kind).IsEqualTo(CalendarDebtDate.PaidOff);
            await Assert.That(goal.Kind).IsEqualTo(CalendarDebtDate.Goal);
            await Assert.That(goal.OnTrack).IsTrue();
        }
    }

    private static SaveCalendarEvent Birthday(string title = "Birthday") => new()
    {
        Title = title,
        Date = new DateOnly(2026, 3, 14),
        Recurrence = EventRecurrence.Yearly
    };

    private CalendarService BuildSubject()
    {
        FixedTimeProvider timeProvider = new(new DateTime(2026, 5, 18, 12, 0, 0, DateTimeKind.Utc));
        ICurrentUser currentUser = CurrentUserFor(Username);
        return new CalendarService(currentUser, DbContextFactory, NullLogger<CalendarService>.Instance, timeProvider,
            new SavingsGoalsService(currentUser, DbContextFactory, NullLogger<SavingsGoalsService>.Instance,
                timeProvider),
            new DebtsService(currentUser, DbContextFactory, NullLogger<DebtsService>.Instance, timeProvider,
                new AccountScope(currentUser)),
            new AccountScope(currentUser));
    }
}
