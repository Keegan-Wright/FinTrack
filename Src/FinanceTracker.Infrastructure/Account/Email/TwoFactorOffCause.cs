namespace FinanceTracker.Infrastructure.Account.Email;

/// <summary>
/// How two-factor sign in came to be turned off, for the email saying so, see
/// <see cref="IAccountEmails.TwoFactorOffAsync"/>.
/// </summary>
public enum TwoFactorOffCause
{
    /// <summary>
    /// The user turned it off.
    /// </summary>
    TurnedOff,

    /// <summary>
    /// The user reset their authenticator, which turns it off until the new key is set up.
    /// </summary>
    AuthenticatorReset,

    /// <summary>
    /// An admin turned it off, for someone who lost their phone and their recovery codes.
    /// </summary>
    TurnedOffByAdmin
}
