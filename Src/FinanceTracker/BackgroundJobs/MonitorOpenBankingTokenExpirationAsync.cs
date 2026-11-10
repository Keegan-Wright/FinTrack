using System.Diagnostics;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Telemetry;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace FinanceTracker.BackgroundJobs;

/// <summary>
/// Revokes the tokens of consents that have lapsed, then tells each owner whose consent ends within a week, or has
/// ended, see <see cref="IConsentNotices"/>.
/// </summary>
public class MonitorOpenBankingTokenExpirationAsync : IJob
{
    private readonly IDbContextFactory<FinanceTrackerContext> _dbContextFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MonitorOpenBankingTokenExpirationAsync> _logger;

    public MonitorOpenBankingTokenExpirationAsync(IDbContextFactory<FinanceTrackerContext> dbContextFactory,
        IServiceScopeFactory scopeFactory, TimeProvider timeProvider,
        ILogger<MonitorOpenBankingTokenExpirationAsync> logger)
    {
        _logger = logger;
        _dbContextFactory = dbContextFactory;
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        using Activity? performingMonitoringActivity =
            FinTrackActivitySources.App.StartActivity(nameof(MonitorOpenBankingTokenExpirationAsync));

        await RevokeLapsedConnectionsAsync(performingMonitoringActivity, cancellationToken);
        await NotifyOwnersAsync(performingMonitoringActivity, cancellationToken);

        performingMonitoringActivity?.AddEvent(new ActivityEvent("Monitor OpenBanking Token Expiration completed"));
    }

    private async Task RevokeLapsedConnectionsAsync(Activity? activity, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        DateTime now = _timeProvider.UtcNow;
        List<BankConnection> lapsedConnections = await dbContext.BankConnections
            .Where(x => x.ConsentExpiresAt <= now && x.AccessTokens.Any(t => t.RevokedAt == null))
            .Include(x => x.AccessTokens.Where(t => t.RevokedAt == null))
            .ToListAsync(cancellationToken);

        foreach (BankConnection connection in lapsedConnections)
        {
            activity?.AddEvent(new ActivityEvent(
                $"Consent lapsed for connection {connection.Id} of user {connection.UserId}"));
            connection.RevokeActiveToken(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Raises each owner's consent notices as that owner. One owner failing doesn't stop the rest, it's recorded on
    /// <paramref name="activity"/>.
    /// </summary>
    private async Task NotifyOwnersAsync(Activity? activity, CancellationToken cancellationToken)
    {
        List<Guid> owners;
        await using (FinanceTrackerContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            DateTime now = _timeProvider.UtcNow;
            DateTime warnFrom = now + ConsentNotices.WeekOut;
            DateTime endedSince = ConsentNotices.EndedSince(now);

            // A user an admin has locked isn't told, they couldn't sign in to reconnect
            owners = await dbContext.BankConnections
                .Where(x => x.ConsentExpiresAt <= warnFrom && x.ConsentExpiresAt > endedSince)
                .Where(x => !dbContext.Users.Any(u =>
                    u.Id == x.UserId && u.LockoutEnd == FinanceTrackerUser.LockedByAdminUntil))
                .Select(static x => x.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        foreach (Guid owner in owners)
        {
            // A span for each owner, so a failure shows whose it was
            using Activity? ownerActivity = FinTrackActivitySources.App.StartUserActivity("Send consent notices", owner);
            ownerActivity?.SetTag(TelemetryTags.NotificationsRaised, 0);
            using IDisposable? logScope = _logger.BeginUserScope(owner);
            try
            {
                await using AsyncServiceScope scope = _scopeFactory.CreateUserScope(owner);
                await scope.ServiceProvider.GetRequiredService<IConsentNotices>().NotifyAsync(cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                ownerActivity.RecordError(ex);
            }
        }
    }
}
