using FinanceTracker.Contracts.Services;
using FinanceTracker.Infrastructure;

namespace FinanceTracker;

/// <summary>
/// <see cref="ISyncNotifications"/> for pages rendered on the server, subscribes the scope's user to the in memory
/// <see cref="ISyncNotifier"/> the sync jobs notify.
/// </summary>
public sealed class CurrentUserSyncNotifications : ISyncNotifications
{
    private readonly ICurrentUser _currentUser;
    private readonly ISyncNotifier _syncNotifier;

    public CurrentUserSyncNotifications(ICurrentUser currentUser, ISyncNotifier syncNotifier)
    {
        _currentUser = currentUser;
        _syncNotifier = syncNotifier;
    }

    public Task<IDisposable> SubscribeAsync(Func<Task> onSyncComplete, CancellationToken cancellationToken) =>
        Task.FromResult(_syncNotifier.Subscribe(_currentUser.UserId, onSyncComplete));
}
