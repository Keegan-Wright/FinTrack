namespace FinanceTracker.Contracts.Services;

/// <summary>
/// Tells the signed in user's page when a background sync of their connections finishes, whether they asked for it
/// with <see cref="IAutomationService.RequestSyncAsync"/> or it ran on schedule, and an admin's page when a
/// background job starts or finishes.
/// </summary>
public interface ISyncNotifications
{
    /// <summary>
    /// Calls <paramref name="onSyncComplete"/> each time a sync finishes for the user, until the returned
    /// subscription is disposed.
    /// </summary>
    /// <remarks>
    /// In the browser this connects to the sync hub first, so it can fail like any request.
    /// </remarks>
    Task<IDisposable> SubscribeAsync(Func<Task> onSyncComplete, CancellationToken cancellationToken);

    /// <summary>
    /// Calls <paramref name="onJobChanged"/> with the job's function each time a background job starts or finishes,
    /// until the returned subscription is disposed. Only admins hear about jobs.
    /// </summary>
    /// <remarks>
    /// A job says it has finished just before TickerQ saves how it went, so its run may still read as running.
    /// </remarks>
    Task<IDisposable> SubscribeToJobsAsync(Func<string, Task> onJobChanged, CancellationToken cancellationToken);
}
