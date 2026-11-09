using FinanceTracker.Contracts.Notifications;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// The signed in user's notifications, as the bell and the notifications page show them, and where they're sent.
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
    /// Where each kind goes, whether email can be sent at all, and when alerts are said.
    /// </summary>
    Task<NotificationSettings> GetSettingsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Shows, emails and posts the kinds in <paramref name="settings"/> from now on and no others.
    /// </summary>
    /// <returns>The settings as they are now.</returns>
    Task<NotificationSettings> ChangeSettingsAsync(ChangeNotificationSettings settings,
        CancellationToken cancellationToken);

    /// <summary>
    /// Says budgets, bills, large transactions and low balances as <paramref name="alerts"/> asks from now on.
    /// Accounts that aren't the user's are passed over.
    /// </summary>
    /// <returns>The settings as they are now.</returns>
    Task<NotificationSettings> ChangeAlertsAsync(ChangeAlertSettings alerts, CancellationToken cancellationToken);

    /// <summary>
    /// Emails the user a test, so they see email works and how it looks. Recorded as their last email whether it
    /// sent or not, see <see cref="NotificationSettings.LastEmail"/>.
    /// </summary>
    /// <returns>The settings with how it went, null when it can't be sent: no mail server or no address.</returns>
    Task<NotificationSettings?> SendTestEmailAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Posts the user's notifications to <paramref name="webhook"/> from now on, the kinds in
    /// <see cref="NotificationSettings.Webhooked"/>. A first webhook gets a new signing secret, changing the address
    /// keeps it.
    /// </summary>
    /// <returns>The settings, or why this FinTrack won't post there.</returns>
    Task<WebhookChange> SetWebhookAsync(SetWebhook webhook, CancellationToken cancellationToken);

    /// <summary>
    /// Stops posting to the user's webhook, forgetting its address and secret.
    /// </summary>
    /// <returns>The settings as they are now.</returns>
    Task<NotificationSettings> RemoveWebhookAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Signs posts to the user's webhook with a new secret from now on.
    /// </summary>
    /// <returns>The settings with it, null when they have no webhook.</returns>
    Task<NotificationSettings?> ReplaceWebhookSecretAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Posts a test to the user's webhook, recorded as its last post whether it was delivered or not.
    /// </summary>
    /// <returns>The settings with how it went, null when they have no webhook.</returns>
    Task<NotificationSettings?> SendTestWebhookAsync(CancellationToken cancellationToken);
}
