using FinanceTracker.Domain.Identity;

namespace FinanceTracker.Tokens;

/// <summary>
/// Marks an endpoint for tokens of a scope other than reading the API, and only those, see
/// <see cref="TokenAccessExtensions.UsableWithToken"/>.
/// </summary>
/// <param name="scope">The scope whose tokens may use it.</param>
public sealed class TokenScopeMetadata(TokenScope scope)
{
    /// <summary>
    /// The scope whose tokens may use it.
    /// </summary>
    public TokenScope Scope { get; } = scope;
}
