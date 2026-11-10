using FinanceTracker.Infrastructure;
using FinanceTracker.Telemetry;
using Microsoft.EntityFrameworkCore;
using FinanceTracker.Infrastructure.Automation;
using Quartz;

namespace FinanceTracker.BackgroundJobs;


/// <summary>
/// On demand sync of a single user's connections, queued when they ask for a resync.
/// </summary>
public class SyncUserOpenBankingDetailsAsync : OpenBankingSyncJob, IJob
{
    public SyncUserOpenBankingDetailsAsync(IServiceScopeFactory scopeFactory,
        IDbContextFactory<FinanceTrackerContext> dbContextFactory, ISyncNotifier syncNotifier,
        TimeProvider timeProvider, ILogger<OpenBankingSyncJob> logger, FinTrackMetrics metrics)
        : base(scopeFactory, dbContextFactory, syncNotifier, timeProvider, logger, metrics)
    {
    }

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
