namespace FinanceTracker.Contracts.Tokens;

/// <summary>
/// How the user confirms it's them before making a token.
/// </summary>
public enum TokenConfirmation
{
    /// <summary>
    /// With their password.
    /// </summary>
    Password,

    /// <summary>
    /// By signing in at the identity provider again, for someone without a password here.
    /// </summary>
    SingleSignOn
}
