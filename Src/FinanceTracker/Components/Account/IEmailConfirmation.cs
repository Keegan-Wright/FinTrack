using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure.Notifications.Email;

namespace FinanceTracker.Components.Account;

/// <summary>
/// Emails a user the link that confirms their email. Notifications and password resets are only emailed to a
/// confirmed one.
/// </summary>
public interface IEmailConfirmation
{
    /// <summary>
    /// Sends the link, unless there's nothing to confirm or no way to send it.
    /// </summary>
    /// <returns>Whether it was sent, not when it's confirmed already, there's no email, or no mail server.</returns>
    /// <exception cref="Exception">Sending failed, see <see cref="IMailSender.SendAsync"/>.</exception>
    Task<bool> SendAsync(FinanceTrackerUser user);
}
