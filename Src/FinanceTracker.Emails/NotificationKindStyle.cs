using FinanceTracker.Design;
using FinanceTracker.Domain.Notifications;

namespace FinanceTracker.Emails;

/// <summary>
/// How each kind of notification looks in an email. What it's about, in a colour that says how much it matters.
/// </summary>
public static class NotificationKindStyle
{
    /// <summary>
    /// What it's about, a word or two above the title, e.g. <c>Bank access</c>.
    /// </summary>
    public static string Label(NotificationKind kind) => kind switch
    {
        NotificationKind.ConsentEndingSoon => "Bank access",
        NotificationKind.ConsentEnded => "Bank access",
        NotificationKind.SyncFailed => "Syncing",
        _ => "Notification"
    };

    /// <summary>
    /// Amber for something to do soon, red for something that has stopped working.
    /// </summary>
    public static string Accent(NotificationKind kind) => kind switch
    {
        NotificationKind.ConsentEndingSoon => LightPalette.Warning,
        NotificationKind.ConsentEnded => LightPalette.Error,
        NotificationKind.SyncFailed => LightPalette.Error,
        _ => LightPalette.Primary
    };
}
