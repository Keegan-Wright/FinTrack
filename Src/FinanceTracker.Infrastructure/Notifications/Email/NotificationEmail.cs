using System.Net;
using FinanceTracker.Domain.Notifications;

namespace FinanceTracker.Infrastructure.Notifications.Email;

/// <summary>
/// A notification as an email its title as the subject, its message, and a link to where it's dealt with when the
/// app's address is known (<c>FINTRACK_PUBLIC_URL</c>), otherwise a line saying to open FinTrack.
/// </summary>
public static class NotificationEmail
{
    /// <summary>
    /// Where the user turns these emails off, relative to the app.
    /// </summary>
    public const string SettingsPath = "Notifications";

    public static MailMessage For(Notification notification, string to, Uri? publicUrl)
    {
        ArgumentNullException.ThrowIfNull(notification);

        Uri? link = LinkTo(notification.Link, publicUrl);
        Uri? settings = LinkTo(SettingsPath, publicUrl);

        return new MailMessage(to, notification.Title, Text(notification, link, settings),
            Html(notification, link, settings));
    }

    private static string Text(Notification notification, Uri? link, Uri? settings)
    {
        string action = link is null ? "Open FinTrack to see it." : $"Open it in FinTrack: {link}";
        string footer = settings is null
            ? "You can turn these emails off in FinTrack, under Notifications."
            : $"You can turn these emails off in FinTrack: {settings}";
        return $"{notification.Message}\n\n{action}\n\n--\n{footer}\n";
    }

    private static string Html(Notification notification, Uri? link, Uri? settings)
    {
        string message = WebUtility.HtmlEncode(notification.Message);
        string action = link is null
            ? "<p>Open FinTrack to see it.</p>"
            : $"<p><a href=\"{WebUtility.HtmlEncode(link.ToString())}\">Open it in FinTrack</a></p>";
        string footer = settings is null
            ? "You can turn these emails off in FinTrack, under Notifications."
            : $"You can <a href=\"{WebUtility.HtmlEncode(settings.ToString())}\">turn these emails off in FinTrack</a>.";

        return "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">" +
               $"<title>{WebUtility.HtmlEncode(notification.Title)}</title></head>" +
               "<body style=\"font-family: sans-serif; line-height: 1.5;\">" +
               $"<p>{message}</p>{action}<hr><p style=\"font-size: 0.875em;\">{footer}</p></body></html>";
    }

    private static Uri? LinkTo(string? path, Uri? publicUrl)
    {
        if (publicUrl is null || path is null)
        {
            return null;
        }

        return new Uri(publicUrl, path);
    }
}
