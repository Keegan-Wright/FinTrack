using FinanceTracker.Contracts.Users;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// Everyone who can sign in, for admins who they are, how they sign in, and a reset link for one who's forgotten
/// their password when FinTrack can't email them one.
/// </summary>
public interface IUsersService
{
    /// <summary>
    /// Every user, by username.
    /// </summary>
    IAsyncEnumerable<UserSummary> GetUsersAsync(CancellationToken cancellationToken);

    /// <summary>
    /// A one-time link for <paramref name="userId"/> to choose a new password with.
    /// </summary>
    /// <returns>The link, null when they have no password here to reset, signing in through the identity provider.</returns>
    /// <exception cref="InvalidOperationException">There's no such user.</exception>
    Task<ResetLink?> CreateResetLinkAsync(Guid userId, CancellationToken cancellationToken);
}
