namespace FinanceTracker;

/// <summary>
/// Tells a user's open circuits and browsers that a background sync has finished for them. Subscriptions and hub
/// connections live in memory, so only those on the instance that ran the sync are reached until SignalR gets a
/// backplane.
/// </summary>
public interface ISyncNotifier
{
    /// <summary>
    /// Calls <paramref name="onSyncComplete"/> whenever a sync finishes for <paramref name="userId"/>, until the
    /// returned subscription is disposed.
    /// </summary>
    IDisposable Subscribe(Guid userId, Func<Task> onSyncComplete);

    Task NotifyAsync(Guid userId);
}
