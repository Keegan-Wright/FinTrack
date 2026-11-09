namespace FinanceTracker.Contracts.Notifications;

/// <summary>
/// The kinds of notification to email and to post to the webhook from now on, the others only show under the bell.
/// </summary>
public sealed class ChangeNotificationSettings
{
    /// <summary>
    /// The kinds to email.
    /// </summary>
    public required IReadOnlyList<NotificationKind> Emailed { get; init; }

    /// <summary>
    /// The kinds to post to the webhook, kept for when they set one.
    /// </summary>
    public required IReadOnlyList<NotificationKind> Webhooked { get; init; }
}
