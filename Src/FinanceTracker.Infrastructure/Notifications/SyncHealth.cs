using FinanceTracker.Domain.Notifications;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Notifications;

/// <inheritdoc cref="ISyncHealth"/>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<ISyncHealth>]
public sealed class SyncHealth : ServiceBase<SyncHealth>, ISyncHealth
{
    private readonly TimeProvider _timeProvider;
    private readonly INotifier _notifier;

    /// <param name="currentUser">The signed in user, or who the work runs as.</param>
    /// <param name="financeTrackerContextFactory">Makes the contexts it reads and saves through.</param>
    /// <param name="logger">Its logger.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="notifier">Raises notifications.</param>
    public SyncHealth(ICurrentUser currentUser, IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory,
        ILogger<SyncHealth> logger, TimeProvider timeProvider, INotifier notifier)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
        _notifier = notifier;
    }

    /// <inheritdoc/>
    public async Task RecordSucceededAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        BankConnection? connection = await FindAsync(context, connectionId, cancellationToken);
        if (connection?.SyncFailingSince is null)
        {
            return;
        }

        DateTime failingSince = connection.SyncFailingSince.Value;
        connection.RecordSyncSucceeded();
        await context.SaveChangesAsync(cancellationToken);
        Logger.LogConnectionSyncRecovered(connection.Id, UserId, failingSince);
    }

    /// <inheritdoc/>
    public async Task RecordFailedAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        BankConnection? connection = await FindAsync(context, connectionId, cancellationToken);
        if (connection is null || !connection.RecordSyncFailed(_timeProvider.UtcNow))
        {
            return;
        }

        await context.SaveChangesAsync(cancellationToken);
        Logger.LogConnectionSyncFailing(connection.Id, UserId);

        // Keyed by when this run of failures started, so a later one is told too
        await _notifier.NotifyAsync(NotificationKind.SyncFailed,
            $"sync-failed:{connection.Id:N}:{connection.SyncFailingSince:yyyyMMddHHmmss}",
            $"{connection.Name} couldn't sync",
            $"FinTrack couldn't sync {connection.Name}. It keeps trying on its schedule.",
            ConsentNotices.RenewLink, cancellationToken);
    }

    /// <summary>
    /// The user's connection, null when it's gone.
    /// </summary>
    /// <param name="context">The context to read through.</param>
    /// <param name="connectionId">The connection.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private Task<BankConnection?> FindAsync(FinanceTrackerContext context, Guid connectionId,
        CancellationToken cancellationToken) =>
        context.BankConnections.OwnedBy(UserId).SingleOrDefaultAsync(x => x.Id == connectionId, cancellationToken);
}
