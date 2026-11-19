namespace FinanceTracker.Contracts.Auth;

/// <summary>
/// How long a password must be, for Identity and every form that sets one. Length only, as NIST SP 800-63B advises:
/// 15 characters for a password that may be the only factor, and no rules about which kinds of character.
/// </summary>
public static class PasswordRules
{
    /// <summary>
    /// The fewest characters a password can have.
    /// </summary>
    public const int MinimumLength = 15;

    /// <summary>
    /// The most characters a password can have, long enough for any passphrase or password manager.
    /// </summary>
    public const int MaximumLength = 128;

    /// <summary>
    /// The hint under a new password field, so the rule is known before it's broken.
    /// </summary>
    public const string Hint = "At least 15 characters, a few words together works well";

    /// <summary>
    /// What a form says when the password is too short.
    /// </summary>
    public const string TooShort = "Use at least 15 characters, a few words together is easy to remember";

    /// <summary>
    /// What a form says when the password is too long.
    /// </summary>
    public const string TooLong = "Use no more than 128 characters";
}
