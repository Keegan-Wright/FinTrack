namespace FinanceTracker.Infrastructure.Notifications.Webhooks;

/// <summary>
/// A webhook's address leads only to private network addresses, which this FinTrack doesn't post to, see
/// <see cref="WebhookSettings.AllowPrivateAddresses"/>.
/// </summary>
public sealed class WebhookAddressRefusedException : Exception
{
    public WebhookAddressRefusedException() : base(WebhookAddresses.PrivateRefusal)
    {
    }

    public WebhookAddressRefusedException(string message) : base(message)
    {
    }

    public WebhookAddressRefusedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
