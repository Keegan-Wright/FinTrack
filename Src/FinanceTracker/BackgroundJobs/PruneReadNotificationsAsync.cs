using System.Diagnostics;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Telemetry;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace FinanceTracker.BackgroundJobs;

/// <summary>
/// Deletes every user's notifications they read longer ago than <see cref="NotificationRetention.ReadKeptFor"/>, so
/// the list under the bell doesn't grow forever. Unread ones are kept however old they are. Ones that only went to
/// email or a webhook are never read, so they go that long after they were raised.
/// </summary>
public class PruneReadNotificationsAsync : IJob
{
    private readonly IDbContextFactory<FinanceTrackerContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;

    public PruneReadNotificationsAsync(IDbContextFactory<FinanceTrackerContext> dbContextFactory,
        TimeProvider timeProvider)
    {
        _dbContextFactory = dbContextFactory;
        _timeProvider = timeProvider;
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken) =>
        await PruneAsync(cancellationToken);

    private async Task PruneAsync(CancellationToken cancellationToken)
    {
        using Activity? activity = FinTrackActivitySources.App.StartActivity(nameof(PruneReadNotificationsAsync));

        await using FinanceTrackerContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        DateTime before = _timeProvider.UtcNow - NotificationRetention.ReadKeptFor;
        int pruned = await dbContext.Notifications
            .Where(x => (x.ReadAt != null && x.ReadAt < before) || (!x.InApp && x.Created < before))
            .ExecuteDeleteAsync(cancellationToken);

        activity?.AddEvent(new ActivityEvent($"Pruned {pruned} read notifications"));
    }
}
