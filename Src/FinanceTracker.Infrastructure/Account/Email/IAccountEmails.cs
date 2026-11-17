using FinanceTracker.Infrastructure.Notifications.Email;

namespace FinanceTracker.Infrastructure.Account.Email;

/// <summary>
/// The emails about a local account, each with a one-time link, in FinTrack's branding with a plain text version.
/// </summary>
public interface IAccountEmails
{
    /// <summary>
    /// Asks the user to confirm their email, with the link that confirms it.
    /// </summary>
    /// <param name="to">Who it's sent to.</param>
    /// <param name="link">The link.</param>
    Task<MailMessage> ConfirmEmailAsync(string to, Uri link);

    /// <summary>
    /// The link that lets the user choose a new password.
    /// </summary>
    /// <param name="to">Who it's sent to.</param>
    /// <param name="link">The link.</param>
    Task<MailMessage> ResetPasswordAsync(string to, Uri link);

    /// <summary>
    /// Sent to a new email the user wants to change to, with the link that makes the change.
    /// </summary>
    /// <param name="to">Who it's sent to.</param>
    /// <param name="link">The link.</param>
    Task<MailMessage> ChangeEmailAsync(string to, Uri link);
}
