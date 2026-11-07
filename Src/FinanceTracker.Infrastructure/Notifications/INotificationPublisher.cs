namespace FinanceTracker.Infrastructure.Notifications;

/// <summary>
/// Tells a user's open browsers they have a new notification, so the bell updates without a reload.
/// </summary>
public interface INotificationPublisher
{
    /// <summary>
    /// Never throws, a missed message only leaves the bell's count behind until the page loads again.
    /// </summary>
    Task NotificationAddedAsync(Guid userId);
}
