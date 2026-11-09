using System.Diagnostics;
using System.Linq.Expressions;
using FinanceTracker.Contracts;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Bills;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.OpenBanking;
using FinanceTracker.Infrastructure.OpenBanking.Balances;
using FinanceTracker.Infrastructure.OpenBanking.Transfers;
using FinanceTracker.Infrastructure.PayPeriods;
using FinanceTracker.Infrastructure.Rules;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.BackgroundJobs;

/// <summary>
/// Syncs a set of connections, each as its owner, updates each owner's balance snapshots for the day, applies their
/// rules and matches their transfers. Tells those owners as the sync starts and once it has finished.
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
    /// snapshots, applies their rules and matches transfers once, so net worth, rules and pairs cover all their banks.
    /// A connection that fails to sync, or an owner whose snapshot, rules or matching fail, doesn't stop the rest.
    /// </summary>
    /// <returns>The owners of the synced connections.</returns>
    protected async Task<IReadOnlySet<Guid>> SyncConnectionsAsync(Expression<Func<BankConnection, bool>> connections,
        CancellationToken cancellationToken)
    {
        using Activity? performingBackgroundSyncActivity = Telemetry.ActivitySource.StartActivity(GetType().Name);

        DateTime startedAt = _timeProvider.UtcNow;
        DateOnly today = _timeProvider.Today;

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

        // Every owner's browsers show it syncing, whoever asked or if it's the schedule
        HashSet<Guid> owners = toSync.Select(static x => x.UserId).ToHashSet();
        foreach (Guid owner in owners)
        {
            await _syncNotifier.NotifyStartedAsync(owner);
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
                await scope.ServiceProvider.GetRequiredService<ISyncHealth>()
                    .RecordSucceededAsync(connectionId, cancellationToken);

                performingBackgroundSyncActivity?.AddEvent(
                    new ActivityEvent($"Finished Processing Connection {connectionId} for User {userId}"));
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                performingBackgroundSyncActivity?.AddException(ex);
                await RecordFailedAsync(connectionId, userId, performingBackgroundSyncActivity, cancellationToken);
            }
        }

        foreach (Guid userId in owners)
        {
            // Rules on what the sync brought in or changed, before transfers so the matcher sees what they decided
            await RunAsOwnerAsync<IRuleApplicationService>(userId, "Applied rules", performingBackgroundSyncActivity,
                x => x.ApplyChangesAsync(startedAt, cancellationToken));

            await RunAsOwnerAsync<ITransferMatchingService>(userId, "Matched transfers",
                performingBackgroundSyncActivity, x => x.MatchChangesAsync(cancellationToken));

            // Bills after rules, so the matcher sees which bills rules said were paid
            await RunAsOwnerAsync<IBillSyncService>(userId, "Synced bills", performingBackgroundSyncActivity,
                x => x.SyncAsync(cancellationToken));

            // After transfers, so money moved from the user's own accounts isn't taken as pay
            await RunAsOwnerAsync<IPaySyncService>(userId, "Synced pay", performingBackgroundSyncActivity,
                x => x.SyncAsync(cancellationToken));

            // After the rules, so net worth counts the debt payments they linked
            await RunAsOwnerAsync<IBalanceSnapshotService>(userId, "Snapshot balances",
                performingBackgroundSyncActivity, x => x.SnapshotAsync(today, startedAt, cancellationToken));

            // After today's snapshot, so newly connected accounts are worked back from it
            await RunAsOwnerAsync<IBalanceSnapshotService>(userId, "Backfilled balance history",
                performingBackgroundSyncActivity, x => x.BackfillAsync(cancellationToken));

            // Last, so they're told about bills, budgets, payments and balances as everything above left them
            await NotifyOwnerAsync(userId, performingBackgroundSyncActivity, cancellationToken);
        }

        performingBackgroundSyncActivity?.AddEvent(new ActivityEvent("Background sync completed"));

        return owners;
    }

    /// <summary>
    /// Tells the owner a connection has started failing to sync, once until it syncs again, see
    /// <see cref="ISyncHealth"/>. A failure to say so is recorded on <paramref name="activity"/>, the other
    /// connections still sync.
    /// </summary>
    private async Task RecordFailedAsync(Guid connectionId, Guid userId, Activity? activity,
        CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateUserScope(userId);
            await scope.ServiceProvider.GetRequiredService<ISyncHealth>()
                .RecordFailedAsync(connectionId, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            activity?.AddException(ex);
        }
    }

    /// <summary>
    /// Runs every <see cref="ISyncNotices"/> as <paramref name="userId"/>. One failing is recorded on
    /// <paramref name="activity"/> and the rest still run.
    /// </summary>
    private async Task NotifyOwnerAsync(Guid userId, Activity? activity, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateUserScope(userId);
        foreach (ISyncNotices notices in scope.ServiceProvider.GetServices<ISyncNotices>())
        {
            try
            {
                    await notices.NotifyAsync(cancellationToken);
                activity?.AddEvent(new ActivityEvent($"Ran {notices.GetType().Name} for User {userId}"));
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                activity?.AddException(ex);
            }
        }
    }

    /// <summary>
    /// Runs <paramref name="run"/> on a <typeparamref name="TService"/> resolved in a scope acting as
    /// <paramref name="userId"/>, as these services work for the current user. A failure is recorded on
    /// <paramref name="activity"/> rather than thrown, so it doesn't stop the other steps or owners.
    /// </summary>
    /// <param name="done">What the step did, for the activity event e.g. <c>Applied rules</c>.</param>
    private async Task RunAsOwnerAsync<TService>(Guid userId, string done, Activity? activity,
        Func<TService, Task> run) where TService : notnull
    {
        try
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateUserScope(userId);
            await run(scope.ServiceProvider.GetRequiredService<TService>());

            activity?.AddEvent(new ActivityEvent($"{done} for User {userId}"));
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
