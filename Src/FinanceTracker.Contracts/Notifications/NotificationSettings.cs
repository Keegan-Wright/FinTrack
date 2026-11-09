namespace FinanceTracker.Contracts.Notifications;

/// <summary>
/// Where the user's notifications go: in the app, by email, and to their webhook. Also when budgets, bills, large
/// transactions and low balances are said.
/// </summary>
public sealed class NotificationSettings
{
    /// <summary>
    /// The kinds shown under the bell and on the Notifications page.
    /// </summary>
    public required IReadOnlyList<NotificationKind> InApp { get; init; }

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

    /// <summary>
    /// How far through a budget's money this month to warn, as a percentage. Going over is always said.
    /// </summary>
    public required int BudgetWarningPercent { get; init; }

    /// <summary>
    /// How many days before a bill is due to remind them.
    /// </summary>
    public required int BillReminderDays { get; init; }

    /// <summary>
    /// Money going out over this is said, null when it never is.
    /// </summary>
    public decimal? LargeTransactionOver { get; init; }

    /// <summary>
    /// Each of the user's accounts and how low it can go before they're told.
    /// </summary>
    public required IReadOnlyList<AccountLowBalance> LowBalance { get; init; }
}
