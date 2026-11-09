using FinanceTracker.Domain.Notifications;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Notifications.Email.Templates;

namespace FinanceTracker.Infrastructure.Notifications.Email;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Singleton<INotificationEmails>]
public sealed class NotificationEmails : INotificationEmails
{
    /// <summary>
    /// Where the user changes which emails they get, relative to the app.
    /// </summary>
    public const string SettingsPath = "Notifications";

    public const string TestSubject = "FinTrack test email";

    private readonly IEmailRenderer _renderer;
    private readonly PublicAddress _publicAddress;

    public NotificationEmails(IEmailRenderer renderer, PublicAddress publicAddress)
    {
        _renderer = renderer;
        _publicAddress = publicAddress;
    }

    public async Task<MailMessage> ForAsync(Notification notification, string to)
    {
        ArgumentNullException.ThrowIfNull(notification);

        Uri? link = _publicAddress.LinkTo(notification.Link);
        Uri? settings = _publicAddress.LinkTo(SettingsPath);

        string html = await _renderer.RenderAsync<NotificationEmailBody>(new Dictionary<string, object?>
        {
            [nameof(NotificationEmailBody.Label)] = NotificationKindStyle.Label(notification.Kind),
            [nameof(NotificationEmailBody.Accent)] = NotificationKindStyle.Accent(notification.Kind),
            [nameof(NotificationEmailBody.Title)] = notification.Title,
            [nameof(NotificationEmailBody.Message)] = notification.Message,
            [nameof(NotificationEmailBody.Link)] = link,
            [nameof(NotificationEmailBody.Settings)] = settings,
            [nameof(NotificationEmailBody.PublicUrl)] = _publicAddress.Url
        });

        string action = link is null ? "Open FinTrack to see it." : $"Open it in FinTrack: {link}";
        string text = $"{notification.Message}\n\n{action}\n\n--\n{TextFooter(settings)}\n";

        return new MailMessage(to, notification.Title, text, html) { Images = [EmailBrand.Logo] };
    }

    public async Task<MailMessage> TestAsync(string to)
    {
        Uri? settings = _publicAddress.LinkTo(SettingsPath);

        string html = await _renderer.RenderAsync<TestEmailBody>(new Dictionary<string, object?>
        {
            [nameof(TestEmailBody.Settings)] = settings,
            [nameof(TestEmailBody.PublicUrl)] = _publicAddress.Url
        });

        string text = "Email works.\n\nThis is how FinTrack's notifications will reach you, for the kinds you've " +
                      $"chosen.\n\n--\nYou asked for this test from FinTrack. {TextFooter(settings)}\n";

        return new MailMessage(to, TestSubject, text, html) { Images = [EmailBrand.Logo] };
    }

    private static string TextFooter(Uri? settings)
    {
        if (settings is null)
        {
            return "You can change which emails you get in FinTrack, under Notifications.";
        }

        return $"You can change which emails you get in FinTrack: {settings}";
    }
}
