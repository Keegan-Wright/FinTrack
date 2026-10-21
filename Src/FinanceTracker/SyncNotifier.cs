using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace FinanceTracker;

/// <summary>
/// Tells a user's open circuits that a background sync has finished for them. Subscriptions live in memory, so only
/// circuits on the instance that ran the sync are reached.
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

public class SyncNotifier : ISyncNotifier
{
    private readonly ConcurrentDictionary<Guid, ImmutableList<Func<Task>>> _subscribers = new();
    private readonly ILogger<SyncNotifier> _logger;

    public SyncNotifier(ILogger<SyncNotifier> logger)
    {
        _logger = logger;
    }

    public IDisposable Subscribe(Guid userId, Func<Task> onSyncComplete)
    {
        _subscribers.AddOrUpdate(userId, [onSyncComplete], (_, existing) => existing.Add(onSyncComplete));
        return new Subscription(this, userId, onSyncComplete);
    }

    public async Task NotifyAsync(Guid userId)
    {
        if (!_subscribers.TryGetValue(userId, out ImmutableList<Func<Task>>? callbacks))
            return;

        foreach (Func<Task> callback in callbacks)
        {
            try
            {
                await callback();
            }
            catch (Exception ex)
            {
                // One broken circuit shouldn't stop the user's other tabs hearing about the sync
                _logger.LogSyncNotificationFailed(ex, userId);
            }
        }
    }

    private void Unsubscribe(Guid userId, Func<Task> callback)
    {
        // A concurrent subscribe can swap the list out from under us, so retry until the removal lands
        while (_subscribers.TryGetValue(userId, out ImmutableList<Func<Task>>? existing))
        {
            ImmutableList<Func<Task>> remaining = existing.Remove(callback);

            bool removed = remaining.IsEmpty
                ? _subscribers.TryRemove(KeyValuePair.Create(userId, existing))
                : _subscribers.TryUpdate(userId, remaining, existing);

            if (removed)
                return;
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly SyncNotifier _notifier;
        private readonly Guid _userId;
        private readonly Func<Task> _callback;
        private int _disposed;

        public Subscription(SyncNotifier notifier, Guid userId, Func<Task> callback)
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
}
