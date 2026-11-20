using FinanceTracker.Contracts.Tokens;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// The signed in user's personal access tokens, which scripts and other applications read their data with. Making one asks
/// the user to confirm it's them, with their password, or for someone who signs in through the identity provider by
/// signing in there again.
/// </summary>
public interface ITokensService
{
    /// <summary>
    /// The user's tokens that haven't been revoked, and how they confirm it's them to make another.
    /// </summary>
    /// <param name="cancellationToken">Stops the work.</param>
    Task<TokensOverview> GetTokensAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Makes a read only token once the user has confirmed it's them, with their password or by just having signed
    /// in at the identity provider again, which this uses up. The user is emailed to say so.
    /// </summary>
    /// <param name="request">Its name, how long it lasts and, for a password user, their password.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The token to show once, or why it wasn't made. Null when they can't have tokens.</returns>
    Task<CreatedToken?> CreateAsync(CreateToken request, CancellationToken cancellationToken);

    /// <summary>
    /// Stops one of the user's tokens working, straight away. The user is emailed to say so.
    /// </summary>
    /// <param name="id">The token's id.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>Whether it was revoked, not when the user has no such token.</returns>
    Task<bool> RevokeAsync(Guid id, CancellationToken cancellationToken);
}
