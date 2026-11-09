namespace FinanceTracker.Infrastructure.Notifications.Webhooks;

/// <summary>
/// Why a post to a webhook failed, in words for the Notifications page.
/// </summary>
public static class WebhookFailure
{
    public static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is WebhookRejectedException { StatusCode: { } status })
        {
            return $"The webhook answered {(int)status}, not success.";
        }

        if (FindRefusal(exception) is not null)
        {
            return WebhookAddresses.PrivateRefusal;
        }

        if (exception is TaskCanceledException or TimeoutException)
        {
            return $"The webhook didn't answer within {WebhookSender.Timeout.TotalSeconds:0} seconds.";
        }

        if (exception is HttpRequestException)
        {
            return "FinTrack couldn't reach the webhook.";
        }

        return "Something went wrong posting to the webhook.";
    }

    // The handler's refusal comes wrapped in the HttpClient's own exception
    private static WebhookAddressRefusedException? FindRefusal(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is WebhookAddressRefusedException refused)
            {
                return refused;
            }

            exception = exception.InnerException;
        }

        return null;
    }
}
