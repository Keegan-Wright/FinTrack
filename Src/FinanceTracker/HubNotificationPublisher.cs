using FinanceTracker.Api;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Infrastructure.Notifications;
using Microsoft.AspNetCore.SignalR;

namespace FinanceTracker;

/// <summary>
/// <see cref="INotificationPublisher"/> through <see cref="SyncHub"/>, every connection of the user hears about it.
/// </summary>
public sealed class HubNotificationPublisher : INotificationPublisher
{
    private readonly IHubContext<SyncHub> _hubContext;
    private readonly ILogger<HubNotificationPublisher> _logger;

    /// <param name="hubContext">Sends to the user's connections.</param>
    /// <param name="logger">Logs a push that failed.</param>
    public HubNotificationPublisher(IHubContext<SyncHub> hubContext, ILogger<HubNotificationPublisher> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task NotificationAddedAsync(Guid userId)
    {
        try
        {
            await _hubContext.Clients.User(userId.ToString()).SendAsync(SyncHubContract.NotificationAdded);
        }
        catch (Exception ex)
        {
            // Saved all the same, the bell catches up when the page next loads
            _logger.LogNotificationPushFailed(ex, userId);
        }
    }
}
