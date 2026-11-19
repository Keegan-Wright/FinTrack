using FinanceTracker.Contracts.Tokens;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// The signed in user's personal access tokens, which scripts and Home Assistant read their data with. Only for users
/// with a password here, as making one asks for it, and one would let someone who signs in through the identity
/// provider get round it.
/// </summary>
public interface ITokensService
{
    /// <summary>
    /// The user's tokens that haven't been revoked, newest first. Null when they have no password here.
    /// </summary>
    /// <param name="cancellationToken">Stops the work.</param>
    Task<IReadOnlyList<TokenSummary>?> GetTokensAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Makes a read only token once the user's password is right. The user is emailed to say so.
    /// </summary>
    /// <param name="request">Its name, how long it lasts and the user's password.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The token to show once, or why it wasn't made. Null when they have no password here.</returns>
    Task<CreatedToken?> CreateAsync(CreateToken request, CancellationToken cancellationToken);

    /// <summary>
    /// Stops one of the user's tokens working, straight away. The user is emailed to say so.
    /// </summary>
    /// <param name="id">The token's id.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>Whether it was revoked, not when the user has no such token.</returns>
    Task<bool> RevokeAsync(Guid id, CancellationToken cancellationToken);
}
