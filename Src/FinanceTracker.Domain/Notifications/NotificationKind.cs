namespace FinanceTracker.Domain.Notifications;

/// <summary>
/// What a <see cref="Notification"/> is about, which also decides whether the user has it emailed, see
/// <see cref="NotificationSettings"/>.
/// </summary>
public enum NotificationKind
{
    /// <summary>
    /// A bank connection's consent ends within a week, or by tomorrow.
    /// </summary>
    ConsentEndingSoon = 0,

    /// <summary>
    /// A bank connection's consent has ended, so it no longer syncs until it's renewed.
    /// </summary>
    ConsentEnded = 1,

    /// <summary>
    /// A bank connection failed to sync, said once until it syncs again.
    /// </summary>
    SyncFailed = 2,

    /// <summary>
    /// A bill went unpaid past its due date, said once for each due date.
    /// </summary>
    BillLate = 3,

    /// <summary>
    /// A bill was paid a different amount than expected, e.g. its price went up, said once for each due date.
    /// </summary>
    BillChanged = 4,

    /// <summary>
    /// A bill is due within the days the user asked to be reminded, saying when the balance won't cover it.
    /// </summary>
    BillDueSoon = 5,

    /// <summary>
    /// A budget's spending this month reached the percentage of it the user asked to be warned at.
    /// </summary>
    BudgetNearLimit = 6,

    /// <summary>
    /// A budget's spending this month went over what it has.
    /// </summary>
    BudgetOverLimit = 7,

    /// <summary>
    /// Money went out over the amount the user asked to hear about.
    /// </summary>
    LargeTransaction = 8,

    /// <summary>
    /// An account's available balance went below the amount the user set for it.
    /// </summary>
    LowBalance = 9
}
