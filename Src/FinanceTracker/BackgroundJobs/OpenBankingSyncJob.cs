using System.Diagnostics;
using System.Linq.Expressions;
using FinanceTracker.Contracts;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Extensions;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Bills;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.Telemetry;
using FinanceTracker.Infrastructure.OpenBanking;
using FinanceTracker.Infrastructure.OpenBanking.Balances;
using FinanceTracker.Infrastructure.OpenBanking.Transfers;
using FinanceTracker.Infrastructure.PayPeriods;
using FinanceTracker.Infrastructure.Rules;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.BackgroundJobs;

/// <summary>
/// Syncs a set of connections, each as its owner, updates each owner's balance snapshots for the day, applies their
/// rules and matches their transfers. Tells those owners as the sync starts and once it has finished. Connections of
/// a user an admin has locked are skipped.
/// </summary>
public abstract class OpenBankingSyncJob
{
    private readonly IDbContextFactory<FinanceTrackerContext> _dbContextFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISyncNotifier _syncNotifier;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OpenBankingSyncJob> _logger;

    protected OpenBankingSyncJob(IServiceScopeFactory scopeFactory,
        IDbContextFactory<FinanceTrackerContext> dbContextFactory, ISyncNotifier syncNotifier,
        TimeProvider timeProvider, ILogger<OpenBankingSyncJob> logger)
    {
        _logger = logger;
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
            // A user an admin has locked is left alone until they're unlocked, their data catches up then
            await foreach (var connection in dbContext.BankConnections
                               .Where(connections)
                               .Where(x => !dbContext.Users.Any(u =>
                                   u.Id == x.UserId && u.LockoutEnd == FinanceTrackerUser.LockedByAdminUntil))
                               .Select(static x => new { x.Id, x.UserId })
                               .AsAsyncEnumerable().WithCancellation(cancellationToken))
            {
                toSync.Add((connection.Id, connection.UserId));
            }
        }

        performingBackgroundSyncActivity?.SetTag(TelemetryTags.ConnectionCount, toSync.Count);

        // Every owner's browsers show it syncing, whoever asked or if it's the schedule
        HashSet<Guid> owners = toSync.Select(static x => x.UserId).ToHashSet();
        foreach (Guid owner in owners)
        {
            await _syncNotifier.NotifyStartedAsync(owner);
        }

        foreach ((Guid connectionId, Guid userId) in toSync)
        {
            // A span for each connection, so its bank calls and queries sit under it in the trace
            using Activity? connectionActivity = Telemetry.StartUserActivity("Sync connection", userId);
            using IDisposable? logScope = _logger.BeginUserScope(userId);
            connectionActivity?.SetTag(TelemetryTags.ConnectionId, connectionId.ToString());

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
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                connectionActivity.RecordError(ex);
                await RecordFailedAsync(connectionId, userId, connectionActivity, cancellationToken);
            }
        }

        foreach (Guid userId in owners)
        {
            // A span for each owner's steps after their connections synced, each step a span inside it
            using Activity? ownerActivity = Telemetry.StartUserActivity("Update owner", userId);
            using IDisposable? logScope = _logger.BeginUserScope(userId);

            // Rules on what the sync brought in or changed, before transfers so the matcher sees what they decided
            await RunAsOwnerAsync<IRuleApplicationService>(userId, "Apply rules",
                x => x.ApplyChangesAsync(startedAt, cancellationToken));

            await RunAsOwnerAsync<ITransferMatchingService>(userId, "Match transfers",
                x => x.MatchChangesAsync(cancellationToken));

            // Bills after rules, so the matcher sees which bills rules said were paid
            await RunAsOwnerAsync<IBillSyncService>(userId, "Sync bills", x => x.SyncAsync(cancellationToken));

            // After transfers, so money moved from the user's own accounts isn't taken as pay
            await RunAsOwnerAsync<IPaySyncService>(userId, "Sync pay", x => x.SyncAsync(cancellationToken));

            // After the rules, so net worth counts the debt payments they linked
            await RunAsOwnerAsync<IBalanceSnapshotService>(userId, "Snapshot balances",
                x => x.SnapshotAsync(today, startedAt, cancellationToken));

            // After today's snapshot, so newly connected accounts are worked back from it
            await RunAsOwnerAsync<IBalanceSnapshotService>(userId, "Backfill balance history",
                x => x.BackfillAsync(cancellationToken));

            // Last, so they're told about bills, budgets, payments and balances as everything above left them
            await NotifyOwnerAsync(userId, cancellationToken);
        }

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
    /// Runs every <see cref="ISyncNotices"/> as <paramref name="userId"/>, each in its own span. One failing is
    /// recorded on its span and the rest still run.
    /// </summary>
    private async Task NotifyOwnerAsync(Guid userId, CancellationToken cancellationToken)
    {
        using Activity? notifyActivity = Telemetry.StartUserActivity("Send sync notices", userId);

        await using AsyncServiceScope scope = _scopeFactory.CreateUserScope(userId);
        foreach (ISyncNotices notices in scope.ServiceProvider.GetServices<ISyncNotices>())
        {
            using Activity? noticesActivity = Telemetry.StartUserActivity(notices.GetType().Name, userId);

            // Counted up by each notification raised, so a span that raised none says so
            noticesActivity?.SetTag(TelemetryTags.NotificationsRaised, 0);
            try
            {
                await notices.NotifyAsync(cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                noticesActivity.RecordError(ex);
            }
        }
    }

    /// <summary>
    /// Runs <paramref name="run"/> on a <typeparamref name="TService"/> resolved in a scope acting as
    /// <paramref name="userId"/>, as these services work for the current user. A failure is recorded on the step's
    /// span rather than thrown, so it doesn't stop the other steps or owners.
    /// </summary>
    /// <param name="step">The step's span name, e.g. <c>Apply rules</c>.</param>
    private async Task RunAsOwnerAsync<TService>(Guid userId, string step, Func<TService, Task> run)
        where TService : notnull
    {
        using Activity? activity = Telemetry.StartUserActivity(step, userId);
        try
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateUserScope(userId);
            await run(scope.ServiceProvider.GetRequiredService<TService>());
        }
        catch (Exception ex)
        {
            activity.RecordError(ex);
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
