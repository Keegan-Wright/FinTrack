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
    private readonly IDbContextFactory<FinanceTrackerContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;

    public MonitorOpenBankingTokenExpirationAsync(IDbContextFactory<FinanceTrackerContext> dbContextFactory,
        TimeProvider timeProvider)
    {
        _dbContextFactory = dbContextFactory;
        _timeProvider = timeProvider;
    }

    public async Task ExecuteAsync(TickerFunctionContext context,
        CancellationToken cancellationToken = new CancellationToken())
    {
        context.CronOccurrenceOperations.SkipIfAlreadyRunning();

        using Activity? performingMonitoringActivity =
            Telemetry.ActivitySource.StartActivity(nameof(MonitorOpenBankingTokenExpirationAsync));

        await using FinanceTrackerContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        List<BankConnection> lapsedConnections = await dbContext.BankConnections
            .Where(x => x.ConsentExpiresAt <= now && x.AccessTokens.Any(t => t.RevokedAt == null))
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
    }
}
