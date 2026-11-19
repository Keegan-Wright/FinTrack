namespace FinanceTracker.Contracts.Profile;

/// <summary>
/// Whether the signed in user has two-factor sign in on, and what's left of it.
/// </summary>
public sealed class TwoFactorStatus
{
    /// <summary>
    /// Whether signing in with their password also asks for a code from their authenticator app.
    /// </summary>
    public required bool Enabled { get; init; }

    /// <summary>
    /// How many recovery codes they have left. Each works once.
    /// </summary>
    public required int RecoveryCodesLeft { get; init; }

    /// <summary>
    /// Whether this browser is remembered, so it isn't asked for a code.
    /// </summary>
    public required bool BrowserRemembered { get; init; }
}
