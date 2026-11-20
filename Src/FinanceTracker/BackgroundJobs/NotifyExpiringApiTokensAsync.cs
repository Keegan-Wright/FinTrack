using System.Diagnostics;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Telemetry;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace FinanceTracker.BackgroundJobs;

/// <summary>
/// Tells each owner whose API token expires within a week, or has expired, see <see cref="ITokenExpiryNotices"/>.
/// </summary>
public class NotifyExpiringApiTokensAsync : IJob
{
    private readonly IDbContextFactory<FinanceTrackerContext> _dbContextFactory;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NotifyExpiringApiTokensAsync> _logger;

    /// <param name="dbContextFactory">Makes the contexts it reads through.</param>
    /// <param name="serviceScopeFactory">Makes the scopes its services run in.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="logger">Its logger.</param>
    public NotifyExpiringApiTokensAsync(IDbContextFactory<FinanceTrackerContext> dbContextFactory,
        IServiceScopeFactory serviceScopeFactory, TimeProvider timeProvider,
        ILogger<NotifyExpiringApiTokensAsync> logger)
    {
        _dbContextFactory = dbContextFactory;
        _serviceScopeFactory = serviceScopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        using Activity? activity = FinTrackActivitySources.App.StartActivity(nameof(NotifyExpiringApiTokensAsync));

        foreach (Guid owner in await OwnersToTellAsync(cancellationToken))
        {

            using Activity? ownerActivity =
                FinTrackActivitySources.App.StartUserActivity("Send API token notices", owner);

            using IDisposable? logScope = _logger.BeginUserScope(owner);
            try
            {
                await using AsyncServiceScope scope = _serviceScopeFactory.CreateUserScope(owner);
                await scope.ServiceProvider.GetRequiredService<ITokenExpiryNotices>().NotifyAsync(cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                ownerActivity.RecordError(ex);
            }
        }
    }

    /// <summary>
    /// The owners of tokens that expire within a week or have expired lately. A user an admin has locked isn't told,
    /// they couldn't sign in to make a new one.
    /// </summary>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task<List<Guid>> OwnersToTellAsync(CancellationToken cancellationToken)
    {
        DateTime now = _timeProvider.UtcNow;
        DateTime warnFrom = now + TokenExpiryNotices.WeekOut;
        DateTime expiredSince = TokenExpiryNotices.ExpiredSince(now);

        await using FinanceTrackerContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.PersonalAccessTokens
            .Where(x => x.RevokedAt == null && x.ExpiresAt <= warnFrom && x.ExpiresAt > expiredSince)
            .Where(x => !dbContext.Users.Any(u =>
                u.Id == x.UserId && u.LockoutEnd == FinanceTrackerUser.LockedByAdminUntil))
            .Select(static x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
