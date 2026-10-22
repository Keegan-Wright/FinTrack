namespace FinanceTracker.Contracts.Services;

/// <summary>
/// Tells the signed in user's page when a background sync of their connections finishes, whether they asked for it
/// with <see cref="IAutomationService.RequestSyncAsync"/> or it ran on schedule.
/// </summary>
public interface ISyncNotifications
{
    /// <summary>
    /// Calls <paramref name="onSyncComplete"/> each time a sync finishes for the user, until the returned
    /// subscription is disposed. The callback may run on any thread.
    /// </summary>
    /// <remarks>
    /// In the browser this connects to the sync hub first, so it can fail like any request.
    /// </remarks>
    Task<IDisposable> SubscribeAsync(Func<Task> onSyncComplete, CancellationToken cancellationToken);
}
