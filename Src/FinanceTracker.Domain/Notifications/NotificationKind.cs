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
    SyncFailed = 2
}
