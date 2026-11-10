using FinanceTracker.Contracts.Profile;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// The signed in user's own account: their email, confirming it, and changing their password and email.
/// </summary>
public interface IProfileService
{
    /// <summary>
    /// Who they are, their email and whether it's confirmed, and how they sign in.
    /// </summary>
    Task<ProfileDetails> GetProfileAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Emails the user a link that confirms their email.
    /// </summary>
    /// <returns>Whether it was sent; not when it's confirmed already, there's no email, or no mail server.</returns>
    Task<bool> SendEmailConfirmationAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Changes their password, keeping them signed in here.
    /// </summary>
    /// <returns>How it went, null when the account has no password to change.</returns>
    Task<ProfileChange?> ChangePasswordAsync(ChangePassword change, CancellationToken cancellationToken);

    /// <summary>
    /// Changes their email when used with a mail server, by a link sent to the new one, the account keeping the old one until
    /// it's opened. without one, straight away without confirmation.
    /// </summary>
    /// <returns>How it went, null when the account has no password, its email being the identity provider's.</returns>
    Task<ProfileChange?> ChangeEmailAsync(ChangeEmail change, CancellationToken cancellationToken);
}
