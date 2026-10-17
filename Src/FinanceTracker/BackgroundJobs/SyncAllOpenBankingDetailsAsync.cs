using System.Diagnostics;
using FinanceTracker.Contracts;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.OpenBanking;
using Microsoft.EntityFrameworkCore;
using TickerQ.Utilities.Base;
using TickerQ.Utilities.Interfaces;

namespace FinanceTracker.BackgroundJobs;


// ReSharper disable once ClassNeverInstantiated.Global
// Background job, ignore the warning
public class SyncAllOpenBankingDetailsAsync : ITickerFunction
{

    private readonly IDbContextFactory<FinanceTrackerContext> _dbContextFactory;
    private readonly IOpenBankingService _openBankingService;
    private readonly IBackgroundSyncService _syncService;

    public SyncAllOpenBankingDetailsAsync(IOpenBankingService openBankingService,
        IDbContextFactory<FinanceTrackerContext> dbContextFactory, IBackgroundSyncService syncService)
    {
        _openBankingService = openBankingService;
        _dbContextFactory = dbContextFactory;
        _syncService = syncService;
    }

    public async Task ExecuteAsync(TickerFunctionContext context,
        CancellationToken cancellationToken = new CancellationToken())
    {
        context.CronOccurrenceOperations.SkipIfAlreadyRunning();

        using var activity = new ActivitySource("FinanceTracker");

        using var performingBackgroundSyncActivity = activity.StartActivity();

        var connections = new List<(Guid Id, Guid UserId)>();
        await using (FinanceTrackerContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            await foreach (var connection in dbContext.BankConnections
                               .Select(static x => new { x.Id, x.UserId })
                               .AsAsyncEnumerable().WithCancellation(cancellationToken))
            {
                connections.Add((connection.Id, connection.UserId));
            }
        }

        foreach ((Guid connectionId, Guid userId) in connections)
        {
            performingBackgroundSyncActivity?.AddEvent(
                new ActivityEvent($"Processing Connection {connectionId} for User {userId}"));

            await _openBankingService.RunFunctionAsUser(userId, async () =>
            {
                try
                {
                    await _openBankingService.SyncConnectionAsync(connectionId, SyncTypes.All, cancellationToken);

                    performingBackgroundSyncActivity?.AddEvent(
                        new ActivityEvent($"Finished Processing Connection {connectionId} for User {userId}"));
                }
                catch (Exception ex)
                {
                    performingBackgroundSyncActivity?.AddException(ex);
                }
            });
        }

        performingBackgroundSyncActivity?.AddEvent(new ActivityEvent("Background sync completed"));

        await _syncService.NotifySyncComplete();
    }
}
