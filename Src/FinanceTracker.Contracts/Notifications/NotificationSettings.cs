namespace FinanceTracker.Contracts.Notifications;

/// <summary>
/// Where the user's notifications go besides the bell, every one shows there: by email, and to their webhook.
/// </summary>
public sealed class NotificationSettings
{
    /// <summary>
    /// The kinds emailed.
    /// </summary>
    public required IReadOnlyList<NotificationKind> Emailed { get; init; }

    /// <summary>
    /// Where they're emailed, the account's address, null when it has none.
    /// </summary>
    public string? EmailAddress { get; init; }

    /// <summary>
    /// Whether this FinTrack can send email at all, set up by whoever runs it.
    /// </summary>
    public required bool EmailAvailable { get; init; }

    /// <summary>
    /// How the last email to them went, null when nothing has been emailed them.
    /// </summary>
    public DeliveryStatus? LastEmail { get; init; }

    /// <summary>
    /// The kinds posted to their webhook, when they have one.
    /// </summary>
    public required IReadOnlyList<NotificationKind> Webhooked { get; init; }

    /// <summary>
    /// Where their notifications are posted, null when they haven't set a webhook.
    /// </summary>
    public string? WebhookUrl { get; init; }

    /// <summary>
    /// What each post is signed with, for whatever receives it to check, null without a webhook.
    /// </summary>
    public string? WebhookSecret { get; init; }

    /// <summary>
    /// How the last post to their webhook went, null when FinTrack hasn't posted to this one.
    /// </summary>
    public DeliveryStatus? LastWebhook { get; init; }
}
