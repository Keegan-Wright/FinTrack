namespace FinanceTracker.Infrastructure.Notifications.Webhooks;

/// <summary>
/// Posts to a user's webhook.
/// </summary>
public interface IWebhookSender
{
    /// <summary>
    /// Posts <paramref name="payload"/> to <paramref name="url"/> as JSON, signed with <paramref name="secret"/>, see
    /// <see cref="WebhookSignature"/>. One try.
    /// </summary>
    /// <exception cref="WebhookRejectedException">It answered, but not with success.</exception>
    /// <exception cref="HttpRequestException">It couldn't be reached, or its address is refused, see
    /// <see cref="WebhookAddressRefusedException"/>.</exception>
    /// <exception cref="TaskCanceledException">It didn't answer in time.</exception>
    Task SendAsync(string url, string secret, WebhookPayload payload, CancellationToken cancellationToken);
}
