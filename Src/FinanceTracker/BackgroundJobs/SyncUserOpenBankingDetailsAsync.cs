using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Automation;
using FinanceTracker.Telemetry;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace FinanceTracker.BackgroundJobs;

/// <summary>
/// On demand sync of a single user's connections, queued when they ask for a resync.
/// </summary>
public class SyncUserOpenBankingDetailsAsync : OpenBankingSyncJob, IJob
{
    /// <param name="serviceScopeFactory">Makes the scopes its services run in.</param>
    /// <param name="dbContextFactory">Makes the contexts it reads and saves through.</param>
    /// <param name="syncNotifier">Tells browsers about syncs and jobs.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="logger">Its logger.</param>
    /// <param name="metrics">Records its metrics.</param>
    public SyncUserOpenBankingDetailsAsync(IServiceScopeFactory serviceScopeFactory,
        IDbContextFactory<FinanceTrackerContext> dbContextFactory, ISyncNotifier syncNotifier,
        TimeProvider timeProvider, ILogger<OpenBankingSyncJob> logger, FinTrackMetrics metrics)
        : base(serviceScopeFactory, dbContextFactory, syncNotifier, timeProvider, logger, metrics)
    {
    }

    /// <param name="context">The run, whose data says which user.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <exception cref="InvalidOperationException">The run doesn't say which user, see
    /// <see cref="JobNames.UserIdKey"/>.</exception>
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!Guid.TryParse(context.MergedJobDataMap.GetString(JobNames.UserIdKey), out Guid userId))
        {
            throw new InvalidOperationException("A user's sync needs the user");
        }

        await SyncConnectionsAsync(x => x.UserId == userId, cancellationToken);

        // Always tell the user, even with nothing to sync, so their resync doesn't look stuck
        await NotifyAsync([userId]);
    }
}
