using FinanceTracker.Contracts.Profile;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// The signed in user's own account their email and confirming it.
/// </summary>
public interface IProfileService
{
    /// <summary>
    /// The user's email, whether it's confirmed, and whether a link can be sent to confirm it.
    /// </summary>
    Task<EmailStatus> GetEmailStatusAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Emails the user a link that confirms their email.
    /// </summary>
    /// <returns>Whether it was sent. Not when it's confirmed already, there's no email, or no mail server.</returns>
    Task<bool> SendEmailConfirmationAsync(CancellationToken cancellationToken);
}
