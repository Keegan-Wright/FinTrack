using System.Diagnostics;
using System.Linq.Expressions;
using FinanceTracker.Contracts;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.OpenBanking;
using FinanceTracker.Infrastructure.OpenBanking.Transfers;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.BackgroundJobs;

/// <summary>
/// Syncs a set of connections, each as its owner, matches each owner's transfers, then tells those owners the sync has
/// finished.
/// </summary>
public abstract class OpenBankingSyncJob
{
    private readonly IDbContextFactory<FinanceTrackerContext> _dbContextFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISyncNotifier _syncNotifier;

    protected OpenBankingSyncJob(IServiceScopeFactory scopeFactory,
        IDbContextFactory<FinanceTrackerContext> dbContextFactory, ISyncNotifier syncNotifier)
    {
        _scopeFactory = scopeFactory;
        _dbContextFactory = dbContextFactory;
        _syncNotifier = syncNotifier;
    }

    /// <summary>
    /// Syncs every connection matching <paramref name="connections"/>, then matches transfers once for each owner so
    /// pairs across their banks are found. A connection that fails to sync, or an owner whose matching fails, doesn't
    /// stop the rest.
    /// </summary>
    /// <returns>The owners of the synced connections.</returns>
    protected async Task<IReadOnlySet<Guid>> SyncConnectionsAsync(Expression<Func<BankConnection, bool>> connections,
        CancellationToken cancellationToken)
    {
        using Activity? performingBackgroundSyncActivity = Telemetry.ActivitySource.StartActivity(GetType().Name);

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
            await MatchTransfersAsync(userId, performingBackgroundSyncActivity, cancellationToken);
        }

        performingBackgroundSyncActivity?.AddEvent(new ActivityEvent("Background sync completed"));

        return owners;
    }

    private async Task MatchTransfersAsync(Guid userId, Activity? activity, CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateUserScope(userId);
            ITransferMatchingService transferMatchingService =
                scope.ServiceProvider.GetRequiredService<ITransferMatchingService>();

            await transferMatchingService.MatchAsync(cancellationToken);

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
