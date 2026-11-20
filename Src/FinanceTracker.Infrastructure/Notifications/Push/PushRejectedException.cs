using System.Net;

namespace FinanceTracker.Infrastructure.Notifications.Push;

/// <summary>
/// The push service answered, but not with success, nor that the browser is gone.
/// </summary>
public sealed class PushRejectedException : Exception
{
    /// <param name="statusCode">What the push service answered.</param>
    public PushRejectedException(HttpStatusCode statusCode)
        : base($"The push service answered {(int)statusCode}") => StatusCode = statusCode;

    /// <summary>
    /// Says the push service didn't answer with success.
    /// </summary>
    public PushRejectedException() : base("The push service didn't answer with success")
    {
    }

    /// <param name="message">What went wrong.</param>
    public PushRejectedException(string message) : base(message)
    {
    }

    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">What caused it.</param>
    public PushRejectedException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// What the push service answered, null when it isn't known.
    /// </summary>
    public HttpStatusCode? StatusCode { get; }
}
