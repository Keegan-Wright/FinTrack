using System.Net;

namespace FinanceTracker.Infrastructure.Notifications.Webhooks;

/// <summary>
/// The webhook answered, but not with success.
/// </summary>
public sealed class WebhookRejectedException : Exception
{
    public WebhookRejectedException(HttpStatusCode statusCode)
        : base($"The webhook answered {(int)statusCode}") => StatusCode = statusCode;

    public WebhookRejectedException() : base("The webhook didn't answer with success")
    {
    }

    public WebhookRejectedException(string message) : base(message)
    {
    }

    public WebhookRejectedException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public HttpStatusCode? StatusCode { get; }
}
