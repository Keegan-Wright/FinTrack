namespace FinanceTracker.Infrastructure.Notifications;

/// <summary>
/// Whether the current user's bank connections are syncing, telling them once when one starts failing.
/// </summary>
public interface ISyncHealth
{
    /// <summary>
    /// The connection synced, so a failure after this is news again.
    /// </summary>
    /// <param name="connectionId">The connection.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    Task RecordSucceededAsync(Guid connectionId, CancellationToken cancellationToken);

    /// <summary>
    /// The connection failed to sync. Tells the user if it's only just started failing, not on every failed run.
    /// </summary>
    /// <param name="connectionId">The connection.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    Task RecordFailedAsync(Guid connectionId, CancellationToken cancellationToken);
}
