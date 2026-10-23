using FinanceTracker.Contracts.Services;

namespace FinanceTracker;

/// <summary>
/// <see cref="ISyncNotifications"/> for the server prerendering the client's pages. Pages subscribe once rendered in
/// the browser, through the sync hub, so nothing should subscribe here.
/// </summary>
public sealed class PrerenderSyncNotifications : ISyncNotifications
{
    public Task<IDisposable> SubscribeAsync(Func<Task> onSyncComplete, CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "Sync notifications reach the browser through the sync hub, subscribe after the first render.");
}
