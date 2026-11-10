using FinanceTracker.Contracts.Profile;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure.Notifications.Email;

namespace FinanceTracker.Components.Account;

/// <summary>
/// Moves a user to a new email, by a link sent to it when there's a mail server, otherwise straight away.
/// </summary>
public interface IEmailChange
{
    /// <summary>
    /// Changes <paramref name="user"/>'s email to <paramref name="newEmail"/>, or sends the link that does.
    /// </summary>
    /// <exception cref="Exception">Sending the link failed, see <see cref="IMailSender.SendAsync"/>.</exception>
    Task<ProfileChange> RequestAsync(FinanceTrackerUser user, string newEmail);
}
