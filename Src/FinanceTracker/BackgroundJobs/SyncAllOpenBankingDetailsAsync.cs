using FinanceTracker.Infrastructure;
using Microsoft.EntityFrameworkCore;
using TickerQ.Utilities.Base;
using TickerQ.Utilities.Interfaces;

namespace FinanceTracker.BackgroundJobs;


// ReSharper disable once ClassNeverInstantiated.Global
// Background job, ignore the warning

/// <summary>
/// Scheduled sync of every user's connections.
/// </summary>
public class SyncAllOpenBankingDetailsAsync : OpenBankingSyncJob, ITickerFunction
{
    public SyncAllOpenBankingDetailsAsync(IServiceScopeFactory scopeFactory,
        IDbContextFactory<FinanceTrackerContext> dbContextFactory, ISyncNotifier syncNotifier,
        TimeProvider timeProvider)
        : base(scopeFactory, dbContextFactory, syncNotifier, timeProvider)
    {
    }

    public async Task ExecuteAsync(TickerFunctionContext context,
        CancellationToken cancellationToken = new CancellationToken())
    {
        context.CronOccurrenceOperations.SkipIfAlreadyRunning();

        IReadOnlySet<Guid> syncedUsers = await SyncConnectionsAsync(static _ => true, cancellationToken);

        await NotifyAsync(syncedUsers);
    }
}
