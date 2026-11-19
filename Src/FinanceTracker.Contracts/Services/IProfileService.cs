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

    /// <summary>
    /// Whether they have two-factor sign in on, and how many recovery codes are left.
    /// </summary>
    /// <returns>Null when the account has no password, so signs in through the identity provider.</returns>
    Task<TwoFactorStatus?> GetTwoFactorAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The key to add to their authenticator app, made the first time it's asked for. Only while two-factor sign in is
    /// off, so the key of an account using it can't be copied from an unlocked screen.
    /// </summary>
    /// <returns>Null when the account has no password, or two-factor sign in is on.</returns>
    Task<AuthenticatorSetup?> GetAuthenticatorSetupAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Turns on two-factor sign in once their app gives a right code, with 10 new recovery codes.
    /// </summary>
    /// <returns>How it went, null when the account has no password.</returns>
    Task<TwoFactorChange?> EnableAuthenticatorAsync(EnableAuthenticator request, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces their recovery codes with 10 new ones. The old ones stop working.
    /// </summary>
    /// <returns>How it went, null when the account has no password.</returns>
    Task<TwoFactorChange?> MakeRecoveryCodesAsync(ConfirmPassword request, CancellationToken cancellationToken);

    /// <summary>
    /// Turns off two-factor sign in and makes a new key, for a new phone. They set it up again to turn it back on.
    /// </summary>
    /// <returns>How it went, null when the account has no password.</returns>
    Task<TwoFactorChange?> ResetAuthenticatorAsync(ConfirmPassword request, CancellationToken cancellationToken);

    /// <summary>
    /// Turns off two-factor sign in.
    /// </summary>
    /// <returns>How it went, null when the account has no password.</returns>
    Task<TwoFactorChange?> TurnOffTwoFactorAsync(ConfirmPassword request, CancellationToken cancellationToken);

    /// <summary>
    /// Stops remembering this browser, so it's asked for a code at the next sign in.
    /// </summary>
    Task ForgetBrowserAsync(CancellationToken cancellationToken);
}
