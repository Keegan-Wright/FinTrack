using FinanceTracker.Infrastructure;
using Microsoft.EntityFrameworkCore;
using TickerQ.Utilities.Base;
using TickerQ.Utilities.Interfaces;

namespace FinanceTracker.BackgroundJobs;

// ReSharper disable once ClassNeverInstantiated.Global
// Background job, ignore the warning
/// <summary>
/// On demand sync of a single user's connections, queued when they ask for a resync.
/// </summary>
public class SyncUserOpenBankingDetailsAsync : OpenBankingSyncJob, ITickerFunction<SyncUserOpenBankingDetailsRequest>
{
    public SyncUserOpenBankingDetailsAsync(IServiceScopeFactory scopeFactory,
        IDbContextFactory<FinanceTrackerContext> dbContextFactory, ISyncNotifier syncNotifier)
        : base(scopeFactory, dbContextFactory, syncNotifier)
    {
    }

    public async Task ExecuteAsync(TickerFunctionContext<SyncUserOpenBankingDetailsRequest> context,
        CancellationToken cancellationToken = new CancellationToken())
    {
        Guid userId = context.Request.UserId;

        await SyncConnectionsAsync(x => x.UserId == userId, cancellationToken);

        // Always tell the user, even with nothing to sync, so their resync doesn't look stuck
        await NotifyAsync([userId]);
    }
}
