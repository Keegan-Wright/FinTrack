using FinanceTracker.Contracts.Notifications;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// The signed in user's notifications, as the bell and the notifications page show them, and which they have emailed.
/// </summary>
public interface INotificationsService
{
    /// <summary>
    /// The user's notifications, newest first, at most <paramref name="take"/> of them.
    /// </summary>
    IAsyncEnumerable<Notification> GetNotificationsAsync(int take, CancellationToken cancellationToken);

    /// <summary>
    /// How many the user hasn't read.
    /// </summary>
    Task<int> GetUnreadCountAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Marks one read.
    /// </summary>
    /// <returns>Whether it was found among the user's.</returns>
    Task<bool> MarkReadAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Marks every one the user hasn't read.
    /// </summary>
    Task MarkAllReadAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Which kinds the user has emailed, where to, and whether email can be sent at all.
    /// </summary>
    Task<NotificationSettings> GetSettingsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Emails the kinds in <paramref name="settings"/> from now on and no others.
    /// </summary>
    /// <returns>The settings as they are now.</returns>
    Task<NotificationSettings> ChangeSettingsAsync(ChangeNotificationSettings settings,
        CancellationToken cancellationToken);
}
