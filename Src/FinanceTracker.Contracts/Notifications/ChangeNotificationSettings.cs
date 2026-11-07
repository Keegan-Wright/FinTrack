namespace FinanceTracker.Contracts.Notifications;

/// <summary>
/// The kinds of notification to email from now on, the others only show under the bell.
/// </summary>
public sealed class ChangeNotificationSettings
{
    /// <summary>
    /// The kinds to email, the rest only show under the bell.
    /// </summary>
    public required IReadOnlyList<NotificationKind> Emailed { get; init; }
}
