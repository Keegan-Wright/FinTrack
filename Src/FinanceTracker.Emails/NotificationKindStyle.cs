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
        NotificationKind.BillLate => "Bills",
        NotificationKind.BillChanged => "Bills",
        NotificationKind.BillDueSoon => "Bills",
        NotificationKind.BudgetNearLimit => "Budgets",
        NotificationKind.BudgetOverLimit => "Budgets",
        NotificationKind.LargeTransaction => "Payments",
        NotificationKind.LowBalance => "Balances",
        _ => "Notification"
    };

    /// <summary>
    /// Amber for something to do soon or to keep an eye on, red for something that has gone wrong, the app's green
    /// for news.
    /// </summary>
    public static string Accent(NotificationKind kind) => kind switch
    {
        NotificationKind.ConsentEndingSoon => LightPalette.Warning,
        NotificationKind.ConsentEnded => LightPalette.Error,
        NotificationKind.SyncFailed => LightPalette.Error,
        NotificationKind.BillLate => LightPalette.Error,
        NotificationKind.BillChanged => LightPalette.Warning,
        NotificationKind.BillDueSoon => LightPalette.Primary,
        NotificationKind.BudgetNearLimit => LightPalette.Warning,
        NotificationKind.BudgetOverLimit => LightPalette.Error,
        NotificationKind.LargeTransaction => LightPalette.Primary,
        NotificationKind.LowBalance => LightPalette.Warning,
        _ => LightPalette.Primary
    };
}
