using FinanceTracker.Contracts.Notifications;

namespace FinanceTracker.Tests.Unit.Client.Notifications;

/// <summary>
/// Notification settings as the server answers them, every kind going everywhere unless said, emailed to
/// sam@example.test, confirmed unless said.
/// </summary>
internal static class TestNotificationSettings
{
    public const string Address = "sam@example.test";
    public const string Hook = "https://hooks.example.test/fintrack";

    /// <summary>
    /// How many kinds there are, a row of the grid each.
    /// </summary>
    public static readonly int Kinds = Enum.GetValues<NotificationKind>().Length;

    public static NotificationSettings Create(string? emailAddress = Address, bool emailAvailable = true,
        string? webhookUrl = null, DeliveryStatus? lastEmail = null, IReadOnlyList<NotificationKind>? inApp = null,
        IReadOnlyList<NotificationKind>? emailed = null, IReadOnlyList<NotificationKind>? webhooked = null,
        bool emailConfirmed = true)
    {
        string? secret = null;
        if (webhookUrl is not null)
        {
            secret = "secret";
        }

        return new NotificationSettings
        {
            InApp = inApp ?? Enum.GetValues<NotificationKind>(),
            Emailed = emailed ?? Enum.GetValues<NotificationKind>(),
            Webhooked = webhooked ?? Enum.GetValues<NotificationKind>(),
            EmailAddress = emailAddress,
            EmailConfirmed = emailConfirmed,
            EmailAvailable = emailAvailable,
            LastEmail = lastEmail,
            WebhookUrl = webhookUrl,
            WebhookSecret = secret,
            BudgetWarningPercent = 80,
            BillReminderDays = 3,
            LowBalance = []
        };
    }
}
