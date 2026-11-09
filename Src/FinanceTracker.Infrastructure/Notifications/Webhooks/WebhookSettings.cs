namespace FinanceTracker.Infrastructure.Notifications.Webhooks;

/// <summary>
/// Where this FinTrack posts webhooks, from <c>WEBHOOK_ALLOW_PRIVATE</c>.
/// </summary>
public sealed class WebhookSettings
{
    /// <summary>
    /// Whether webhooks can go to private network addresses, e.g. Home Assistant at home. A webhook
    /// is an address any user types in, and the server posting there would let them reach what's on its network.
    /// </summary>
    public bool AllowPrivateAddresses { get; init; }
}
