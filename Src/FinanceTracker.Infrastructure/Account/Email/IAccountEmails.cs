using FinanceTracker.Infrastructure.Notifications.Email;

namespace FinanceTracker.Infrastructure.Account.Email;

/// <summary>
/// The emails about a local account, in FinTrack's branding with a plain text version. Some carry a one-time link,
/// others tell the user how they sign in changed.
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

    /// <summary>
    /// Tells the user two-factor sign in was turned on, so they know if it wasn't them.
    /// </summary>
    /// <param name="to">Who it's sent to.</param>
    Task<MailMessage> TwoFactorOnAsync(string to);

    /// <summary>
    /// Tells the user two-factor sign in was turned off, and how.
    /// </summary>
    /// <param name="to">Who it's sent to.</param>
    /// <param name="cause">How it was turned off.</param>
    Task<MailMessage> TwoFactorOffAsync(string to, TwoFactorOffCause cause);

    /// <summary>
    /// Tells the user new recovery codes were made, so the old ones no longer work.
    /// </summary>
    /// <param name="to">Who it's sent to.</param>
    Task<MailMessage> RecoveryCodesMadeAsync(string to);

    /// <summary>
    /// Tells the user a passkey was added to their account.
    /// </summary>
    /// <param name="to">Who it's sent to.</param>
    /// <param name="name">The passkey's name.</param>
    Task<MailMessage> PasskeyAddedAsync(string to, string name);

    /// <summary>
    /// Tells the user one of their passkeys was removed.
    /// </summary>
    /// <param name="to">Who it's sent to.</param>
    /// <param name="name">The passkey's name.</param>
    Task<MailMessage> PasskeyRemovedAsync(string to, string name);

    /// <summary>
    /// Tells the user an admin removed all their passkeys.
    /// </summary>
    /// <param name="to">Who it's sent to.</param>
    Task<MailMessage> PasskeysRemovedByAdminAsync(string to);
}
