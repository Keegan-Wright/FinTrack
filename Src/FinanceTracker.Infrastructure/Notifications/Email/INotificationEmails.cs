using FinanceTracker.Domain.Notifications;

namespace FinanceTracker.Infrastructure.Notifications.Email;

/// <summary>
/// The emails notifications go out as, in FinTrack's branding with a plain text version.
/// </summary>
public interface INotificationEmails
{
    /// <summary>
    /// The notification as an email its title as the subject, its message, and a link to where it's dealt with when
    /// the app's address is known (<c>FINTRACK_PUBLIC_URL</c>), otherwise a line saying to open FinTrack.
    /// </summary>
    Task<MailMessage> ForAsync(Notification notification, string to);

    /// <summary>
    /// The email the Notifications page sends to show email works.
    /// </summary>
    Task<MailMessage> TestAsync(string to);
}
