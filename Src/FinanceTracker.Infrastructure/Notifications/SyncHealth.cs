using FinanceTracker.Domain.Notifications;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Notifications;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<ISyncHealth>]
public sealed class SyncHealth : ServiceBase<SyncHealth>, ISyncHealth
{
    private readonly TimeProvider _timeProvider;
    private readonly INotifier _notifier;

    public SyncHealth(ICurrentUser currentUser, IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory,
        ILogger<SyncHealth> logger, TimeProvider timeProvider, INotifier notifier)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
        _notifier = notifier;
    }

    public async Task RecordSucceededAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        BankConnection? connection = await FindAsync(context, connectionId, cancellationToken);
        if (connection?.SyncFailingSince is null)
        {
            return;
        }

        connection.RecordSyncSucceeded();
        await context.SaveChangesAsync(cancellationToken);
    }

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

        // Keyed by when this run of failures started, so a later one is told too
        await _notifier.NotifyAsync(NotificationKind.SyncFailed,
            $"sync-failed:{connection.Id:N}:{connection.SyncFailingSince:yyyyMMddHHmmss}",
            $"{connection.Name} couldn't sync",
            $"FinTrack couldn't sync {connection.Name}. It keeps trying on its schedule.",
            ConsentNotices.RenewLink, cancellationToken);
    }

    private Task<BankConnection?> FindAsync(FinanceTrackerContext context, Guid connectionId,
        CancellationToken cancellationToken) =>
        context.BankConnections.OwnedBy(UserId).SingleOrDefaultAsync(x => x.Id == connectionId, cancellationToken);
}
