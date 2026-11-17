using FinanceTracker.Contracts.Services;

namespace FinanceTracker;

/// <summary>
/// <see cref="ISyncNotifications"/> for the server prerendering the client's pages. Pages subscribe once rendered in
/// the browser, through the sync hub, so nothing should subscribe here and every subscription is refused.
/// </summary>
public sealed class PrerenderSyncNotifications : ISyncNotifications
{
    /// <inheritdoc/>
    public Task<IDisposable> SubscribeAsync(Func<Task> onSyncComplete, CancellationToken cancellationToken) =>
        Refused();

    /// <inheritdoc/>
    public Task<IDisposable> SubscribeToSyncStartsAsync(Func<Task> onSyncStarted,
        CancellationToken cancellationToken) => Refused();

    /// <inheritdoc/>
    public Task<IDisposable> SubscribeToJobsAsync(Func<string, Task> onJobChanged,
        CancellationToken cancellationToken) => Refused();

    /// <inheritdoc/>
    public Task<IDisposable> SubscribeToNotificationsAsync(Func<Task> onNotificationAdded,
        CancellationToken cancellationToken) => Refused();

    /// <summary>
    /// Never, nothing is heard while prerendering.
    /// </summary>
    public bool Paused => false;

    /// <summary>
    /// Never raised, nothing is heard while prerendering.
    /// </summary>
    public event EventHandler? PausedChanged
    {
        add { }
        remove { }
    }

    /// <summary>
    /// Refuses a subscription.
    /// </summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    private static Task<IDisposable> Refused() =>
        throw new NotSupportedException(
            "Sync and job notifications reach the browser through the sync hub, subscribe after the first render.");
}
