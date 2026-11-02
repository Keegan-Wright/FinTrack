using FinanceTracker.BackgroundJobs;
using FinanceTracker.Domain.Calendar;
using FinanceTracker.Domain.SavingsGoals;
using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Unit.FinanceTracker.BackgroundJobs;

public class MoveEventSavingsGoalsOnAsyncTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private static readonly DateOnly Birthday = new(2026, 3, 14);

    [Test]
    public async Task ExecuteAsync_TheDayHasPassed_MovesTheGoalOnToNextYear()
    {
        // Arrange
        await SeedAsync(rollsGoalOn: true);
        MoveEventSavingsGoalsOnAsync subject = BuildSubject(new DateTime(2026, 3, 15, 2, 0, 0, DateTimeKind.Utc));

        // Act
        await subject.ExecuteAsync(TickerFunctionContextFactory.Create(), CancellationToken);

        // Assert
        SavingsGoal goal = await LoadGoalAsync();
        using (Assert.Multiple())
        {
            await Assert.That(goal.TargetDate).IsEqualTo(new DateTime(2027, 3, 14));
            await Assert.That(goal.Target).IsEqualTo(100m);
        }
    }

    [Test]
    public async Task ExecuteAsync_OnTheDay_LeavesTheGoal()
    {
        // Arrange
        await SeedAsync(rollsGoalOn: true);
        MoveEventSavingsGoalsOnAsync subject = BuildSubject(new DateTime(2026, 3, 14, 2, 0, 0, DateTimeKind.Utc));

        // Act
        await subject.ExecuteAsync(TickerFunctionContextFactory.Create(), CancellationToken);

        // Assert
        SavingsGoal goal = await LoadGoalAsync();
        await Assert.That(goal.TargetDate).IsEqualTo(Birthday.ToDateTime(TimeOnly.MinValue));
    }

    [Test]
    public async Task ExecuteAsync_EventNotRollingOn_LeavesTheGoal()
    {
        // Arrange
        await SeedAsync(rollsGoalOn: false);
        MoveEventSavingsGoalsOnAsync subject = BuildSubject(new DateTime(2026, 6, 1, 2, 0, 0, DateTimeKind.Utc));

        // Act
        await subject.ExecuteAsync(TickerFunctionContextFactory.Create(), CancellationToken);

        // Assert
        SavingsGoal goal = await LoadGoalAsync();
        using (Assert.Multiple())
        {
            await Assert.That(goal.TargetDate).IsEqualTo(Birthday.ToDateTime(TimeOnly.MinValue));
            await Assert.That(goal.Target).IsEqualTo(60m);
        }
    }

    private async Task SeedAsync(bool rollsGoalOn)
    {
        SavingsGoal? gifts = null;
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.SavingsGoalScenario.WithSavingsGoal("gifts", Username, userId =>
                gifts = new SavingsGoal(userId, "Gifts", 0m, 0m, 60m, Birthday.ToDateTime(TimeOnly.MinValue))))
            .Do(s => s.CalendarEventScenario.WithEvent("birthday", Username, userId =>
            {
                CalendarEvent calendarEvent = new(userId, "Birthday", Birthday, EventRecurrence.Yearly);
                calendarEvent.SetSavingsTarget(100m);
                calendarEvent.LinkSavingsGoal(gifts!);
                calendarEvent.SetRollsGoalOn(rollsGoalOn);
                return calendarEvent;
            }));
    }

    private async Task<SavingsGoal> LoadGoalAsync()
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        return await context.SavingsGoals.SingleAsync(x => x.Id == Scenario.SavingsGoalMappings["gifts"],
            CancellationToken);
    }

    private MoveEventSavingsGoalsOnAsync BuildSubject(DateTime now) => new(DbContextFactory, new FixedTimeProvider(now));
}
