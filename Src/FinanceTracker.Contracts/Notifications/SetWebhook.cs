namespace FinanceTracker.Contracts.Notifications;

/// <summary>
/// Where to post the user's notifications, see <see cref="Validation.SetWebhookValidator"/>.
/// </summary>
public sealed class SetWebhook
{
    /// <summary>
    /// The webhook's address, http or https.
    /// </summary>
    public string Url { get; set; } = "";
}
