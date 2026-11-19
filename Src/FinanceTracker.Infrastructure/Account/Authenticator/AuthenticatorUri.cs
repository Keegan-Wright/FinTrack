using System.Globalization;

namespace FinanceTracker.Infrastructure.Account.Authenticator;

/// <summary>
/// The <c>otpauth://</c> URI an authenticator app reads from the QR code, naming the settings so the app makes codes
/// the same way.
/// </summary>
public static class AuthenticatorUri
{
    /// <summary>
    /// The URI for <paramref name="key"/>, labelled with <paramref name="account"/>.
    /// </summary>
    /// <param name="account">Who the key is for, shown in the app.</param>
    /// <param name="key">The base32 key without spaces.</param>
    public static string For(string account, string key)
    {
        string issuer = Uri.EscapeDataString(AuthenticatorSettings.Issuer);
        return string.Create(CultureInfo.InvariantCulture,
            $"otpauth://totp/{issuer}:{Uri.EscapeDataString(account)}?secret={key}&issuer={issuer}" +
            $"&algorithm={AuthenticatorSettings.Algorithm}&digits={AuthenticatorSettings.Digits}" +
            $"&period={AuthenticatorSettings.StepSeconds}");
    }
}
