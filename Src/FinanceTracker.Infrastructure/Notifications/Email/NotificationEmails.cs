using FinanceTracker.Domain.Notifications;
using FinanceTracker.Emails;
using FinanceTracker.Emails.Templates;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Infrastructure.Notifications.Email;

/// <inheritdoc cref="INotificationEmails"/>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Singleton<INotificationEmails>]
public sealed class NotificationEmails : INotificationEmails
{
    /// <summary>
    /// Where the user changes which emails they get, relative to the app.
    /// </summary>
    public const string SettingsPath = "Notifications/Settings";

    /// <summary>
    /// The test email's subject.
    /// </summary>
    public const string TestSubject = TestEmailBody.Subject;

    private readonly IEmailRenderer _emailRenderer;
    private readonly PublicAddress _publicAddress;

    /// <param name="emailRenderer">Renders the emails.</param>
    /// <param name="publicAddress">The app's address as people reach it.</param>
    public NotificationEmails(IEmailRenderer emailRenderer, PublicAddress publicAddress)
    {
        _emailRenderer = emailRenderer;
        _publicAddress = publicAddress;
    }

    /// <inheritdoc/>
    public async Task<MailMessage> ForAsync(Notification notification, string to)
    {
        ArgumentNullException.ThrowIfNull(notification);

        Uri? link = _publicAddress.LinkTo(notification.Link);
        Uri? settings = _publicAddress.LinkTo(SettingsPath);

        string html = await _emailRenderer.RenderAsync<NotificationEmailBody>(new Dictionary<string, object?>
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

    /// <inheritdoc/>
    public async Task<MailMessage> TestAsync(string to)
    {
        Uri? settings = _publicAddress.LinkTo(SettingsPath);

        string html = await _emailRenderer.RenderAsync<TestEmailBody>(new Dictionary<string, object?>
        {
            [nameof(TestEmailBody.Settings)] = settings,
            [nameof(TestEmailBody.PublicUrl)] = _publicAddress.Url
        });

        string text = "Email works.\n\nThis is how FinTrack's notifications will reach you, for the kinds you've " +
                      $"chosen.\n\n--\nYou asked for this test from FinTrack. {TextFooter(settings)}\n";

        return new MailMessage(to, TestSubject, text, html) { Images = [EmailBrand.Logo] };
    }

    /// <summary>
    /// The plain text footer, saying where to change which emails come.
    /// </summary>
    /// <param name="settings">The settings page, null when the app's address isn't known.</param>
    private static string TextFooter(Uri? settings)
    {
        if (settings is null)
        {
            return "You can change which emails you get in FinTrack, under Notifications.";
        }

        return $"You can change which emails you get in FinTrack: {settings}";
    }
}
