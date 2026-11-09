namespace FinanceTracker.Contracts.Notifications;

/// <summary>
/// The kinds of notification to show in the app, to email and to post to the webhook from now on. A kind in none of
/// them isn't raised.
/// </summary>
public sealed class ChangeNotificationSettings
{
    /// <summary>
    /// The kinds to show under the bell and on the Notifications page.
    /// </summary>
    public required IReadOnlyList<NotificationKind> InApp { get; init; }

    /// <summary>
    /// The kinds to email.
    /// </summary>
    public required IReadOnlyList<NotificationKind> Emailed { get; init; }

    /// <summary>
    /// The kinds to post to the webhook, kept for when they set one.
    /// </summary>
    public required IReadOnlyList<NotificationKind> Webhooked { get; init; }
}
