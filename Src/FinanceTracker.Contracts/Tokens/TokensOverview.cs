namespace FinanceTracker.Contracts.Tokens;

/// <summary>
/// The user's personal access tokens, and how they confirm it's them to make another.
/// </summary>
public sealed class TokensOverview
{
    /// <summary>
    /// How they confirm it's them, null when they can't have tokens, as they can't sign in with a password or through
    /// the identity provider.
    /// </summary>
    public required TokenConfirmation? Confirmation { get; init; }

    /// <summary>
    /// Whether they've just signed in at the identity provider again, so can make a token now without doing it again.
    /// Only with <see cref="TokenConfirmation.SingleSignOn"/>.
    /// </summary>
    public required bool Confirmed { get; init; }

    /// <summary>
    /// Their tokens that haven't been revoked, newest first.
    /// </summary>
    public IReadOnlyList<TokenSummary> Tokens { get; init; } = [];
}
