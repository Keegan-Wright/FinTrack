namespace FinanceTracker;

/// <summary>
/// A <see cref="SyncNotifier"/> subscription, disposing it unsubscribes. Safe to dispose more than once.
/// </summary>
internal sealed class SyncNotifierSubscription : IDisposable
{
    private readonly SyncNotifier _notifier;
    private readonly Guid _userId;
    private readonly Func<Task> _callback;
    private int _disposed;

    public SyncNotifierSubscription(SyncNotifier notifier, Guid userId, Func<Task> callback)
    {
        _notifier = notifier;
        _userId = userId;
        _callback = callback;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            _notifier.Unsubscribe(_userId, _callback);
    }
}
