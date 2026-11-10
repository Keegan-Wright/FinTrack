using FinanceTracker.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace FinanceTracker.BackgroundJobs;

/// <summary>
/// Scheduled sync of every user's connections.
/// </summary>
public class SyncAllOpenBankingDetailsAsync : OpenBankingSyncJob, IJob
{
    public SyncAllOpenBankingDetailsAsync(IServiceScopeFactory scopeFactory,
        IDbContextFactory<FinanceTrackerContext> dbContextFactory, ISyncNotifier syncNotifier,
        TimeProvider timeProvider, ILogger<OpenBankingSyncJob> logger)
        : base(scopeFactory, dbContextFactory, syncNotifier, timeProvider, logger)
    {
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        IReadOnlySet<Guid> syncedUsers = await SyncConnectionsAsync(static _ => true, cancellationToken);

        await NotifyAsync(syncedUsers);
    }
}
