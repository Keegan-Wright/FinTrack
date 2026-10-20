using System.Diagnostics;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Infrastructure;
using Microsoft.EntityFrameworkCore;
using TickerQ.Utilities.Base;
using TickerQ.Utilities.Interfaces;

namespace FinanceTracker.BackgroundJobs;


// ReSharper disable once ClassNeverInstantiated.Global
// Background job, ignore the warning
public class MonitorOpenBankingTokenExpirationAsync : ITickerFunction
{
    /// <summary>
    /// Open banking consent must be renewed by the user every 90 days.
    /// </summary>
    private static readonly TimeSpan ConsentLifetime = TimeSpan.FromDays(90);

    private readonly IDbContextFactory<FinanceTrackerContext> _dbContextFactory;
    private readonly IBackgroundSyncService _syncService;

    public MonitorOpenBankingTokenExpirationAsync(IDbContextFactory<FinanceTrackerContext> dbContextFactory, IBackgroundSyncService syncService)
    {
        _dbContextFactory = dbContextFactory;
        _syncService = syncService;
    }

    public async Task ExecuteAsync(TickerFunctionContext context,
        CancellationToken cancellationToken = new CancellationToken())
    {
        context.CronOccurrenceOperations.SkipIfAlreadyRunning();

        using var activity = new ActivitySource("FinanceTracker");

        using var performingMonitoringActivity = activity.StartActivity();

        await using FinanceTrackerContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        DateTime now = DateTime.UtcNow;
        DateTime lapsedBefore = now - ConsentLifetime;

        List<BankConnection> lapsedConnections = await dbContext.BankConnections
            .Where(x => x.ConsentGrantedAt <= lapsedBefore && x.AccessTokens.Any(t => t.RevokedAt == null))
            .Include(x => x.AccessTokens.Where(t => t.RevokedAt == null))
            .ToListAsync(cancellationToken);

        foreach (BankConnection connection in lapsedConnections)
        {
            performingMonitoringActivity?.AddEvent(new ActivityEvent($"Consent lapsed for connection {connection.Id} of user {connection.UserId}"));

            connection.RevokeActiveToken(now);
            //TODO Notification
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        performingMonitoringActivity?.AddEvent(new ActivityEvent("Monitor OpenBanking Token Expiration completed"));

        await _syncService.NotifySyncComplete();
    }
}
