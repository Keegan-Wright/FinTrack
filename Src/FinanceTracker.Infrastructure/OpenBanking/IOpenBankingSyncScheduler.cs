namespace FinanceTracker.Infrastructure.OpenBanking;

/// <summary>
/// Queues background syncs. Implemented by the host, which owns the job scheduler and the sync jobs.
/// </summary>
public interface IOpenBankingSyncScheduler
{
    /// <summary>
    /// Queues a sync of all <paramref name="userId"/>'s connections, to run as soon as possible.
    /// </summary>
    /// <param name="userId">The user to sync.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    Task ScheduleUserSyncAsync(Guid userId, CancellationToken cancellationToken);
}
