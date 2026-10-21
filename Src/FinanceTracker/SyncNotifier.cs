using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace FinanceTracker;

/// <summary>
/// In memory <see cref="ISyncNotifier"/>, subscriptions are keyed by user.
/// </summary>
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
        return new SyncNotifierSubscription(this, userId, onSyncComplete);
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

    internal void Unsubscribe(Guid userId, Func<Task> callback)
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
}
