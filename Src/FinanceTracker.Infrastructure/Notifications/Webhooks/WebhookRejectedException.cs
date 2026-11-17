using System.Net;

namespace FinanceTracker.Infrastructure.Notifications.Webhooks;

/// <summary>
/// The webhook answered, but not with success.
/// </summary>
public sealed class WebhookRejectedException : Exception
{
    /// <param name="statusCode">What the webhook answered.</param>
    public WebhookRejectedException(HttpStatusCode statusCode)
        : base($"The webhook answered {(int)statusCode}") => StatusCode = statusCode;

    public WebhookRejectedException() : base("The webhook didn't answer with success")
    {
    }

    /// <param name="message">What went wrong.</param>
    public WebhookRejectedException(string message) : base(message)
    {
    }

    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">What caused it.</param>
    public WebhookRejectedException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public HttpStatusCode? StatusCode { get; }
}
