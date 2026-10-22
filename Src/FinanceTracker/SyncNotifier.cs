using System.Collections.Concurrent;
using System.Collections.Immutable;
using FinanceTracker.Api;
using FinanceTracker.Contracts.Api;
using Microsoft.AspNetCore.SignalR;

namespace FinanceTracker;

/// <summary>
/// <see cref="ISyncNotifier"/> for both front ends in memory subscriptions keyed by user for server rendered
/// circuits, and <see cref="SyncHub"/> for browsers calling the API.
/// </summary>
public class SyncNotifier : ISyncNotifier
{
    private readonly ConcurrentDictionary<Guid, ImmutableList<Func<Task>>> _subscribers = new();
    private readonly IHubContext<SyncHub> _hubContext;
    private readonly ILogger<SyncNotifier> _logger;

    public SyncNotifier(IHubContext<SyncHub> hubContext, ILogger<SyncNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public IDisposable Subscribe(Guid userId, Func<Task> onSyncComplete)
    {
        _subscribers.AddOrUpdate(userId, [onSyncComplete], (_, existing) => existing.Add(onSyncComplete));
        return new SyncNotifierSubscription(this, userId, onSyncComplete);
    }

    public async Task NotifyAsync(Guid userId)
    {
        try
        {
            await _hubContext.Clients.User(userId.ToString()).SendAsync(SyncHubContract.SyncComplete);
        }
        catch (Exception ex)
        {
            // Browsers missing the push shouldn't stop the user's circuits hearing about the sync
            _logger.LogSyncNotificationFailed(ex, userId);
        }

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
