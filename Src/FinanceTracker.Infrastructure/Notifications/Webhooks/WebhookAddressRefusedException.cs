namespace FinanceTracker.Infrastructure.Notifications.Webhooks;

/// <summary>
/// A webhook's address leads only to private network addresses, which this FinTrack doesn't post to, see
/// <see cref="WebhookSettings.AllowPrivateAddresses"/>.
/// </summary>
public sealed class WebhookAddressRefusedException : Exception
{
    /// <summary>
    /// Says the address is private.
    /// </summary>
    public WebhookAddressRefusedException() : base(WebhookAddresses.PrivateRefusal)
    {
    }

    /// <param name="message">What went wrong.</param>
    public WebhookAddressRefusedException(string message) : base(message)
    {
    }

    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">What caused it.</param>
    public WebhookAddressRefusedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
