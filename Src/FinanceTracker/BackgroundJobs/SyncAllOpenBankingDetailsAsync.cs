using FinanceTracker.Infrastructure;
using FinanceTracker.Telemetry;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace FinanceTracker.BackgroundJobs;

/// <summary>
/// Scheduled sync of every user's connections.
/// </summary>
public class SyncAllOpenBankingDetailsAsync : OpenBankingSyncJob, IJob
{
    /// <param name="serviceScopeFactory">Makes the scopes its services run in.</param>
    /// <param name="dbContextFactory">Makes the contexts it reads and saves through.</param>
    /// <param name="syncNotifier">Tells browsers about syncs and jobs.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="logger">Its logger.</param>
    /// <param name="metrics">Records its metrics.</param>
    public SyncAllOpenBankingDetailsAsync(IServiceScopeFactory serviceScopeFactory,
        IDbContextFactory<FinanceTrackerContext> dbContextFactory, ISyncNotifier syncNotifier,
        TimeProvider timeProvider, ILogger<OpenBankingSyncJob> logger, FinTrackMetrics metrics)
        : base(serviceScopeFactory, dbContextFactory, syncNotifier, timeProvider, logger, metrics)
    {
    }

    /// <inheritdoc/>
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        IReadOnlySet<Guid> syncedUsers = await SyncConnectionsAsync(static _ => true, cancellationToken);

        await NotifyAsync(syncedUsers);
    }
}
