namespace FinanceTracker.Contracts.Tokens;

/// <summary>
/// A new personal access token, read only, for another app or script.
/// </summary>
public sealed class CreateToken
{
    /// <summary>
    /// What it's for, so the user can tell their tokens apart.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// How long it lasts.
    /// </summary>
    public TokenExpiry Expiry { get; set; } = TokenExpiry.NinetyDays;

    /// <summary>
    /// The user's password, as a token reads their data without two-factor sign in or a passkey. Empty for someone
    /// who confirms through the identity provider instead.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
