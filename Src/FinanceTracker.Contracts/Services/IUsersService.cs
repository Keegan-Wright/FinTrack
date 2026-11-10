using FinanceTracker.Contracts.Users;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// Everyone who can sign in, for admins who they are, how they sign in, a reset link for one who's forgotten
/// their password when FinTrack can't email them one, and locking one out.
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

    /// <summary>
    /// Locks <paramref name="userId"/> out until they're unlocked. They're signed out wherever they're signed in,
    /// and their banks aren't synced meanwhile.
    /// </summary>
    /// <returns>Whether they were locked, not when they're the admin asking, who can't lock themselves out.</returns>
    /// <exception cref="InvalidOperationException">There's no such user.</exception>
    Task<bool> LockAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Lets <paramref name="userId"/> sign in again, lifting a lock and any lockout from wrong passwords.
    /// </summary>
    /// <exception cref="InvalidOperationException">There's no such user.</exception>
    Task UnlockAsync(Guid userId, CancellationToken cancellationToken);
}
