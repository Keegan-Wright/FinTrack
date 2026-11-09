namespace FinanceTracker.Contracts.Notifications;

/// <summary>
/// How setting a webhook went. The settings with it, or why this FinTrack won't post there.
/// </summary>
public sealed class WebhookChange
{
    /// <summary>
    /// The settings with the webhook, null when it was refused.
    /// </summary>
    public NotificationSettings? Settings { get; init; }

    /// <summary>
    /// Why it was refused, e.g. it's on a private network this FinTrack doesn't post to, null when it was set.
    /// </summary>
    public string? Refusal { get; init; }
}
