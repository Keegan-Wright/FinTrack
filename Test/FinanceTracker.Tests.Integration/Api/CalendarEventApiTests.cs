using System.Net;
using FinanceTracker.Contracts.Calendar;
using FinanceTracker.Contracts.SavingsGoals;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using DomainCalendarEvent = FinanceTracker.Domain.Calendar.CalendarEvent;

namespace FinanceTracker.Tests.Integration.Api;

public class CalendarEventApiTests : IntegrationTestBase
{
    [Test]
    public async Task AddEvent_ShowsEachYearCanBeChangedAndDeleted()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        ICalendarService service = ApiFor(user).Get<ICalendarService>();
        SaveCalendarEvent birthday = Birthday();
        birthday.SavingsTarget = 100m;
        birthday.CreateSavingsGoal = true;
        birthday.RollsGoalOn = true;

        // Act
        CalendarEvent added = await service.AddEventAsync(birthday, CancellationToken);
        List<CalendarItems> march = await service.GetMonthItemsAsync(3, 2030, CancellationToken)
            .ToListAsync(CancellationToken);
        SaveCalendarEvent renamed = Birthday();
        renamed.Title = "Mum's birthday";
        CalendarEvent updated = await service.UpdateEventAsync(added.Id, renamed, CancellationToken);
        await service.DeleteEventAsync(added.Id, CancellationToken);
        List<CalendarEvent> afterDelete = await service.GetEventsAsync(CancellationToken)
            .ToListAsync(CancellationToken);
        List<SavingsGoal> goals = await ApiFor(user).Get<ISavingsGoalsService>()
            .GetSavingsGoalsAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert
        CalendarEventItem onTheDay = march.Single(x => x.Date.Day == 14).Events.Single();
        using (Assert.Multiple())
        {
            await Assert.That(added.SavingsGoalName).IsEqualTo("Birthday");
            await Assert.That(added.RollsGoalOn).IsTrue();
            await Assert.That(onTheDay.SavingsGoalName).IsEqualTo("Birthday");
            await Assert.That(onTheDay.Saved).IsEqualTo(0m);
            await Assert.That(onTheDay.SavingsTarget).IsEqualTo(100m);
            await Assert.That(updated.Title).IsEqualTo("Mum's birthday");
            await Assert.That(updated.SavingsGoalId).IsNull();
            await Assert.That(afterDelete).IsEmpty();
            await Assert.That(goals.Select(static x => x.Name)).IsEquivalentTo(["Birthday"]);
        }
    }

    [Test]
    public async Task AddEvent_Invalid_IsABadRequest()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        ICalendarService service = ApiFor(user).Get<ICalendarService>();

        // Act
        HttpRequestException? exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.AddEventAsync(new SaveCalendarEvent { Title = "No date" }, CancellationToken));

        // Assert
        await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task AddEvent_AnotherUsersGoal_ThrowsAsTheServiceDoes()
    {
        // Arrange
        SignedInUser owner = await RegisterUserAsync();
        SignedInUser other = await RegisterUserAsync();
        await Scenario.Do(s => s.SavingsGoalScenario.WithSavingsGoal("theirs", owner.Username));
        SaveCalendarEvent birthday = Birthday();
        birthday.SavingsGoalId = Scenario.SavingsGoalMappings["theirs"];

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ApiFor(other).Get<ICalendarService>().AddEventAsync(birthday, CancellationToken));
    }

    [Test]
    public async Task Events_AreStoredEncryptedAndReadBack()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        SaveCalendarEvent birthday = Birthday();
        birthday.Notes = "Book a table";
        CalendarEvent added = await ApiFor(user).Get<ICalendarService>().AddEventAsync(birthday, CancellationToken);

        // Act
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        string storedTitle = await context.Database
            .SqlQueryRaw<string>("SELECT \"Title\" AS \"Value\" FROM \"CalendarEvents\" WHERE \"Id\" = {0}", added.Id)
            .SingleAsync(CancellationToken);
        string storedNotes = await context.Database
            .SqlQueryRaw<string>("SELECT \"Notes\" AS \"Value\" FROM \"CalendarEvents\" WHERE \"Id\" = {0}", added.Id)
            .SingleAsync(CancellationToken);
        DomainCalendarEvent loaded = await context.CalendarEvents.SingleAsync(x => x.Id == added.Id, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(storedTitle).DoesNotContain("Birthday");
            await Assert.That(storedNotes).DoesNotContain("Book a table");
            await Assert.That(loaded.Title).IsEqualTo("Birthday");
            await Assert.That(loaded.Notes).IsEqualTo("Book a table");
            await Assert.That(loaded.Date).IsEqualTo(new DateOnly(2026, 3, 14));
        }
    }

    private static SaveCalendarEvent Birthday() => new()
    {
        Title = "Birthday",
        Date = new DateOnly(2026, 3, 14),
        Recurrence = EventRecurrence.Yearly
    };
}
