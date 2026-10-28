using System.Diagnostics;
using System.Linq.Expressions;
using FinanceTracker.Contracts;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.OpenBanking;
using FinanceTracker.Infrastructure.OpenBanking.Balances;
using FinanceTracker.Infrastructure.OpenBanking.Transfers;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.BackgroundJobs;

/// <summary>
/// Syncs a set of connections, each as its owner, updates each owner's balance snapshots for the day and matches their
/// transfers, then tells those owners the sync has finished.
/// </summary>
public abstract class OpenBankingSyncJob
{
    private readonly IDbContextFactory<FinanceTrackerContext> _dbContextFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISyncNotifier _syncNotifier;
    private readonly TimeProvider _timeProvider;

    protected OpenBankingSyncJob(IServiceScopeFactory scopeFactory,
        IDbContextFactory<FinanceTrackerContext> dbContextFactory, ISyncNotifier syncNotifier,
        TimeProvider timeProvider)
    {
        _scopeFactory = scopeFactory;
        _dbContextFactory = dbContextFactory;
        _syncNotifier = syncNotifier;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Syncs every connection matching <paramref name="connections"/>, then for each owner updates today's balance
    /// snapshots and matches transfers once, so net worth and pairs cover all their banks. A connection that fails to
    /// sync, or an owner whose snapshot or matching fails, doesn't stop the rest.
    /// </summary>
    /// <returns>The owners of the synced connections.</returns>
    protected async Task<IReadOnlySet<Guid>> SyncConnectionsAsync(Expression<Func<BankConnection, bool>> connections,
        CancellationToken cancellationToken)
    {
        using Activity? performingBackgroundSyncActivity = Telemetry.ActivitySource.StartActivity(GetType().Name);

        DateTime startedAt = _timeProvider.GetUtcNow().UtcDateTime;
        DateOnly today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

        var toSync = new List<(Guid Id, Guid UserId)>();
        await using (FinanceTrackerContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            await foreach (var connection in dbContext.BankConnections
                               .Where(connections)
                               .Select(static x => new { x.Id, x.UserId })
                               .AsAsyncEnumerable().WithCancellation(cancellationToken))
            {
                toSync.Add((connection.Id, connection.UserId));
            }
        }

        foreach ((Guid connectionId, Guid userId) in toSync)
        {
            performingBackgroundSyncActivity?.AddEvent(
                new ActivityEvent($"Processing Connection {connectionId} for User {userId}"));

            try
            {
                // Each connection syncs in its own scope acting as its owner, so services resolve that user and
                // nothing carries over from the previous connection
                await using AsyncServiceScope scope = _scopeFactory.CreateUserScope(userId);
                IOpenBankingSyncService openBankingSyncService =
                    scope.ServiceProvider.GetRequiredService<IOpenBankingSyncService>();

                await openBankingSyncService.SyncConnectionAsync(connectionId, SyncTypes.All, cancellationToken);

                performingBackgroundSyncActivity?.AddEvent(
                    new ActivityEvent($"Finished Processing Connection {connectionId} for User {userId}"));
            }
            catch (Exception ex)
            {
                performingBackgroundSyncActivity?.AddException(ex);
            }
        }

        HashSet<Guid> owners = toSync.Select(static x => x.UserId).ToHashSet();
        foreach (Guid userId in owners)
        {
            await SnapshotBalancesAsync(userId, today, startedAt, performingBackgroundSyncActivity, cancellationToken);
            await MatchTransfersAsync(userId, performingBackgroundSyncActivity, cancellationToken);
        }

        performingBackgroundSyncActivity?.AddEvent(new ActivityEvent("Background sync completed"));

        return owners;
    }

    /// <summary>
    /// Updates the owner's snapshots for <paramref name="today"/>, in the server's time zone, from the balances the
    /// sync stored.
    /// </summary>
    private async Task SnapshotBalancesAsync(Guid userId, DateOnly today, DateTime syncStartedAt, Activity? activity,
        CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateUserScope(userId);
            IBalanceSnapshotService balanceSnapshotService =
                scope.ServiceProvider.GetRequiredService<IBalanceSnapshotService>();

            await balanceSnapshotService.SnapshotAsync(today, syncStartedAt, cancellationToken);

            activity?.AddEvent(new ActivityEvent($"Snapshot balances for User {userId}"));
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
        }
    }

    private async Task MatchTransfersAsync(Guid userId, Activity? activity, CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateUserScope(userId);
            ITransferMatchingService transferMatchingService =
                scope.ServiceProvider.GetRequiredService<ITransferMatchingService>();

            await transferMatchingService.MatchChangesAsync(cancellationToken);

            activity?.AddEvent(new ActivityEvent($"Matched transfers for User {userId}"));
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
        }
    }

    protected async Task NotifyAsync(IEnumerable<Guid> userIds)
    {
        foreach (Guid userId in userIds)
        {
            await _syncNotifier.NotifyAsync(userId);
        }
    }
}
