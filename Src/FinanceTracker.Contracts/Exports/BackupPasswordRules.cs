using FinanceTracker.Contracts.Auth;

namespace FinanceTracker.Contracts.Exports;

/// <summary>
/// How long a backup's password must be. Longer than a sign in password, as a zip's password is turned into its key
/// with PBKDF2-HMAC-SHA1 at only 1000 iterations, which the format fixes so other tools can open it. Guessing is cheap
/// against a copied file, so the length is what protects it.
/// </summary>
public static class BackupPasswordRules
{
    /// <summary>
    /// The fewest characters a backup's password can have.
    /// </summary>
    public const int MinimumLength = 16;

    /// <summary>
    /// The most characters a backup's password can have, as for a sign in password.
    /// </summary>
    public const int MaximumLength = PasswordRules.MaximumLength;

    /// <summary>
    /// The hint under the password field, so the rule is known before it's broken.
    /// </summary>
    public const string Hint = "At least 16 characters, a few words together works well";

    /// <summary>
    /// What the form says when the password is too short.
    /// </summary>
    public const string TooShort = "Use at least 16 characters, a few words together is easy to remember";

    /// <summary>
    /// What the form says when the password is too long.
    /// </summary>
    public const string TooLong = PasswordRules.TooLong;
}
