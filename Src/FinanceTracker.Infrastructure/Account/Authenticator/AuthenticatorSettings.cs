using FinanceTracker.Contracts.Profile;

namespace FinanceTracker.Infrastructure.Account.Authenticator;

/// <summary>
/// How authenticator app codes are made and checked, in one place for the app, the QR code and the tests. HMAC-SHA256
/// rather than the SHA-1 Identity and many apps default to.
/// </summary>
public static class AuthenticatorSettings
{
    /// <summary>
    /// The HMAC hash, as the <c>otpauth://</c> URI names it.
    /// </summary>
    public const string Algorithm = "SHA256";

    /// <summary>
    /// The length of a new key in bytes. 256 bits, the size of a SHA-256 hash. RFC 2104 recommends.
    /// </summary>
    public const int KeyBytes = 32;

    /// <summary>
    /// How many digits a code has.
    /// </summary>
    public const int Digits = TwoFactorCodes.Length;

    /// <summary>
    /// How long each code lasts in seconds.
    /// </summary>
    public const int StepSeconds = 30;

    /// <summary>
    /// How many steps either side of now a code is still taken from, for a phone clock that's a little out. RFC 6238
    /// recommends no more than one.
    /// </summary>
    public const int AllowedDriftSteps = 1;

    /// <summary>
    /// The name authenticator apps show beside the code.
    /// </summary>
    public const string Issuer = "FinTrack";
}
