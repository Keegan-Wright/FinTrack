using FinanceTracker.Contracts.Notifications;
using MudBlazor;

namespace FinanceTracker.Client.Components.Notifications;

/// <summary>
/// How each kind of notification reads and looks the icon beside it.
/// </summary>
public static class NotificationKindText
{
    /// <summary>
    /// Every kind, in the order the email settings list them.
    /// </summary>
    public static IReadOnlyList<NotificationKind> All { get; } = Enum.GetValues<NotificationKind>();

    /// <summary>
    /// What the kind is about, as the email settings put it, e.g. <c>A bank's access ending soon</c>.
    /// </summary>
    public static string EmailLabel(NotificationKind kind) => kind switch
    {
        NotificationKind.ConsentEndingSoon => "A bank's access ending soon",
        NotificationKind.ConsentEnded => "A bank's access having ended",
        NotificationKind.SyncFailed => "A bank failing to sync",
        _ => kind.ToString()
    };

    public static string Icon(NotificationKind kind) => kind switch
    {
        NotificationKind.ConsentEndingSoon => Icons.Material.Outlined.HourglassBottom,
        NotificationKind.ConsentEnded => Icons.Material.Outlined.LinkOff,
        NotificationKind.SyncFailed => Icons.Material.Outlined.SyncProblem,
        _ => Icons.Material.Outlined.Notifications
    };
}
