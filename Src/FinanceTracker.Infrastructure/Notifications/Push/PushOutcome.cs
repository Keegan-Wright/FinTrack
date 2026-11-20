namespace FinanceTracker.Infrastructure.Notifications.Push;

/// <summary>
/// How a push went, when the push service answered.
/// </summary>
public enum PushOutcome
{
    /// <summary>
    /// The push service took it.
    /// </summary>
    Delivered,

    /// <summary>
    /// The browser has unsubscribed or its subscription ran out, so the device should be removed.
    /// </summary>
    Gone
}
