using FinanceTracker.Contracts.Tokens;

namespace FinanceTracker.Client.Components.Tokens;

/// <summary>
/// What the API tokens page shows, whether the user can have tokens and the ones they have.
/// </summary>
public sealed class TokenList
{
    /// <summary>
    /// Whether the user can have tokens, only with a password here.
    /// </summary>
    public required bool Allowed { get; init; }

    /// <summary>
    /// Their tokens that haven't been revoked, newest first.
    /// </summary>
    public List<TokenSummary> Tokens { get; init; } = [];
}
