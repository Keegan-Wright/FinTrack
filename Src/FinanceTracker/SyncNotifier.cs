using FinanceTracker.Api;
using FinanceTracker.Contracts.Api;
using Microsoft.AspNetCore.SignalR;

namespace FinanceTracker;

/// <summary>
/// <see cref="ISyncNotifier"/> through <see cref="SyncHub"/>, every connection of the user hears about the sync.
/// </summary>
public class SyncNotifier : ISyncNotifier
{
    private readonly IHubContext<SyncHub> _hubContext;
    private readonly ILogger<SyncNotifier> _logger;

    public SyncNotifier(IHubContext<SyncHub> hubContext, ILogger<SyncNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyAsync(Guid userId)
    {
        try
        {
            await _hubContext.Clients.User(userId.ToString()).SendAsync(SyncHubContract.SyncComplete);
        }
        catch (Exception ex)
        {
            // The sync itself succeeded, a missed notification only leaves the page's sync time stale
            _logger.LogSyncNotificationFailed(ex, userId);
        }
    }
}
