namespace FinanceTracker.Contracts.Services;

/// <summary>
/// Tells the signed in user's page when a background sync of their connections starts and finishes, whether they asked for it
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
    /// In the browser this connects to the sync hub first. One that can't be reached doesn't fail the subscription,
    /// it's heard once the hub is back, see <see cref="Paused"/>.
    /// </remarks>
    Task<IDisposable> SubscribeAsync(Func<Task> onSyncComplete, CancellationToken cancellationToken);

    /// <summary>
    /// Calls <paramref name="onSyncStarted"/> each time a sync of the user's banks starts, on its schedule or asked
    /// for from any of their browsers, until the returned subscription is disposed.
    /// </summary>
    /// <remarks>
    /// In the browser this connects to the sync hub first. One that can't be reached doesn't fail the subscription,
    /// it's heard once the hub is back, see <see cref="Paused"/>.
    /// </remarks>
    Task<IDisposable> SubscribeToSyncStartsAsync(Func<Task> onSyncStarted, CancellationToken cancellationToken);

    /// <summary>
    /// Calls <paramref name="onJobChanged"/> with the job's function each time a background job starts or finishes,
    /// until the returned subscription is disposed. Only admins hear about jobs.
    /// </summary>
    /// <remarks>
    /// A job says it has started or finished once its run is saved, so the run reads as it is.
    /// </remarks>
    Task<IDisposable> SubscribeToJobsAsync(Func<string, Task> onJobChanged, CancellationToken cancellationToken);

    /// <summary>
    /// Calls <paramref name="onNotificationAdded"/> each time the user has a new notification, until the returned
    /// subscription is disposed.
    /// </summary>
    /// <remarks>
    /// In the browser this connects to the sync hub first. One that can't be reached doesn't fail the subscription,
    /// it's heard once the hub is back, see <see cref="Paused"/>.
    /// </remarks>
    Task<IDisposable> SubscribeToNotificationsAsync(Func<Task> onNotificationAdded,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether live updates are paused, the connection to the sync hub was lost, or couldn't be made, and is being
    /// tried again. Syncs and jobs that finish meanwhile aren't heard about.
    /// </summary>
    bool Paused { get; }

    /// <summary>
    /// Raised when <see cref="Paused"/> changes.
    /// </summary>
    event EventHandler? PausedChanged;
}
