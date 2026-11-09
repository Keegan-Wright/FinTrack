namespace FinanceTracker.Infrastructure.Notifications;

/// <summary>
/// How long notifications are kept. Read ones are deleted once they've been read this long, unread ones are kept until
/// they're read. Ones not shown in the app are deleted this long after they were raised.
/// </summary>
/// <remarks>
/// A notification's key is what stops its event being raised again, so deleting it lets that event be raised afresh.
/// Anything that keeps noticing an event that stays true, e.g. a consent that has ended, stops noticing it once it's
/// been true longer than <see cref="ReadKeptFor"/>. A deleted notification was raised at least that long ago, so it
/// can't come back.
/// </remarks>
public static class NotificationRetention
{
    /// <summary>
    /// How long a read notification is kept after it was read.
    /// </summary>
    public static readonly TimeSpan ReadKeptFor = TimeSpan.FromDays(90);
}
