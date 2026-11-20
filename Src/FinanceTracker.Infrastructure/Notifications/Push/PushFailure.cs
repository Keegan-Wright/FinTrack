using FinanceTracker.Infrastructure.Notifications.Webhooks;

namespace FinanceTracker.Infrastructure.Notifications.Push;

/// <summary>
/// Why a push failed, in words for the Notifications page.
/// </summary>
public static class PushFailure
{
    /// <summary>
    /// What went wrong, for the user.
    /// </summary>
    /// <param name="exception">What pushing threw.</param>
    public static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is PushRejectedException { StatusCode: { } status })
        {
            return $"The push service answered {(int)status}, not success.";
        }

        if (IsAddressRefused(exception))
        {
            return "The push service is on a private network, which this FinTrack doesn't send to.";
        }

        if (exception is TaskCanceledException or TimeoutException)
        {
            return $"The push service didn't answer within {PushSender.Timeout.TotalSeconds:0} seconds.";
        }

        if (exception is HttpRequestException)
        {
            return "FinTrack couldn't reach the push service.";
        }

        return "Something went wrong sending the push.";
    }

    /// <summary>
    /// Whether the address was refused, which the client wraps in its own exception.
    /// </summary>
    /// <param name="exception">What pushing threw.</param>
    private static bool IsAddressRefused(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is WebhookAddressRefusedException)
            {
                return true;
            }

            exception = exception.InnerException;
        }

        return false;
    }
}
