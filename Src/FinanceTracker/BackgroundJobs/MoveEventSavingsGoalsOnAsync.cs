using System.Diagnostics;
using FinanceTracker.Domain.Calendar;
using FinanceTracker.Domain.SavingsGoals;
using FinanceTracker.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace FinanceTracker.BackgroundJobs;

/// <summary>
/// Moves on the savings goal of each yearly event that rolls its goal on, once the day it was saving for has
/// passed, see <see cref="CalendarEvent.MoveGoalOn"/>.
/// </summary>
public class MoveEventSavingsGoalsOnAsync : IJob
{
    private readonly IDbContextFactory<FinanceTrackerContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;

    public MoveEventSavingsGoalsOnAsync(IDbContextFactory<FinanceTrackerContext> dbContextFactory,
        TimeProvider timeProvider)
    {
        _dbContextFactory = dbContextFactory;
        _timeProvider = timeProvider;
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken) =>
        await MoveGoalsOnAsync(cancellationToken);

    private async Task MoveGoalsOnAsync(CancellationToken cancellationToken)
    {
        using Activity? activity = Telemetry.ActivitySource.StartActivity(nameof(MoveEventSavingsGoalsOnAsync));

        await using FinanceTrackerContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Rolling on and the goal are kept in the clear, so only these events are loaded
        List<CalendarEvent> rolling = await dbContext.CalendarEvents
            .Where(static x => x.RollsGoalOn && x.SavingsGoalId != null)
            .ToListAsync(cancellationToken);

        List<Guid> goalIds = rolling.Select(static x => x.SavingsGoalId!.Value).Distinct().ToList();
        Dictionary<Guid, SavingsGoal> goals = await dbContext.SavingsGoals
            .Where(x => goalIds.Contains(x.Id))
            .ToDictionaryAsync(static x => x.Id, cancellationToken);

        DateOnly today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
        int moved = 0;
        foreach (CalendarEvent calendarEvent in rolling)
        {
            if (goals.TryGetValue(calendarEvent.SavingsGoalId!.Value, out SavingsGoal? goal) &&
                calendarEvent.MoveGoalOn(goal, today))
            {
                moved++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        activity?.AddEvent(new ActivityEvent($"Moved on {moved} event savings goals"));
    }
}
