using FinanceTracker.Contracts.Notifications;
using MudBlazor;

namespace FinanceTracker.Client.Components.Notifications;

/// <summary>
/// How each kind of notification reads and looks the icon beside it.
/// </summary>
public static class NotificationKindText
{
    /// <summary>
    /// Every kind, in the order the settings list them.
    /// </summary>
    public static IReadOnlyList<NotificationKind> All { get; } = Enum.GetValues<NotificationKind>();

    /// <summary>
    /// What the kind is about, as the settings put it, e.g. <c>A bank's access ending soon</c>.
    /// </summary>
    public static string Label(NotificationKind kind) => kind switch
    {
        NotificationKind.ConsentEndingSoon => "A bank's access ending soon",
        NotificationKind.ConsentEnded => "A bank's access having ended",
        NotificationKind.SyncFailed => "A bank failing to sync",
        NotificationKind.BillLate => "A bill going unpaid",
        NotificationKind.BillChanged => "A bill costing a different amount",
        NotificationKind.BillDueSoon => "A bill coming up",
        NotificationKind.BudgetNearLimit => "A budget nearing its limit",
        NotificationKind.BudgetOverLimit => "A budget going over",
        NotificationKind.LargeTransaction => "A large payment",
        NotificationKind.LowBalance => "An account running low",
        NotificationKind.SharingInvite => "Someone inviting you to share",
        NotificationKind.SharingAccepted => "Someone accepting your invite to share",
        _ => kind.ToString()
    };

    public static string Icon(NotificationKind kind) => kind switch
    {
        NotificationKind.ConsentEndingSoon => Icons.Material.Outlined.HourglassBottom,
        NotificationKind.ConsentEnded => Icons.Material.Outlined.LinkOff,
        NotificationKind.SyncFailed => Icons.Material.Outlined.SyncProblem,
        NotificationKind.BillLate => Icons.Material.Outlined.EventBusy,
        NotificationKind.BillChanged => Icons.Material.Outlined.PriceChange,
        NotificationKind.BillDueSoon => Icons.Material.Outlined.Event,
        NotificationKind.BudgetNearLimit => Icons.Material.Outlined.DataUsage,
        NotificationKind.BudgetOverLimit => Icons.Material.Outlined.MoneyOff,
        NotificationKind.LargeTransaction => Icons.Material.Outlined.Payments,
        NotificationKind.LowBalance => Icons.Material.Outlined.AccountBalanceWallet,
        NotificationKind.SharingInvite => Icons.Material.Outlined.GroupAdd,
        NotificationKind.SharingAccepted => Icons.Material.Outlined.Handshake,
        _ => Icons.Material.Outlined.Notifications
    };
}
