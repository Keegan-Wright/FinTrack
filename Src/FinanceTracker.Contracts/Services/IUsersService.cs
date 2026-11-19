using FinanceTracker.Contracts.Users;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// Everyone who can sign in, for admins who they are, how they sign in, a reset link for one who's forgotten
/// their password when FinTrack can't email them one, locking one out, and who else is an admin.
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
    /// <exception cref="NotFoundException">There's no such user.</exception>
    Task<ResetLink?> CreateResetLinkAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Locks <paramref name="userId"/> out until they're unlocked. They're signed out wherever they're signed in,
    /// and their banks aren't synced meanwhile.
    /// </summary>
    /// <returns>Whether they were locked, not when they're an admin, the one asking included. Admins can't lock each
    /// other, their role is taken away first.</returns>
    /// <exception cref="NotFoundException">There's no such user.</exception>
    Task<bool> LockAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Lets <paramref name="userId"/> sign in again, lifting a lock and any lockout from wrong passwords.
    /// </summary>
    /// <exception cref="NotFoundException">There's no such user.</exception>
    Task UnlockAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Makes <paramref name="userId"/> an admin. Their sign in picks it up within a minute.
    /// </summary>
    /// <returns>How it went, refused for someone the identity provider's admin group decides for.</returns>
    /// <exception cref="NotFoundException">There's no such user.</exception>
    Task<AdminChange> GrantAdminAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Takes the admin role from <paramref name="userId"/>, the one asking included. Their sign in drops it within a
    /// minute.
    /// </summary>
    /// <returns>How it went, refused for someone the identity provider's admin group decides for, or for the last
    /// admin while password sign in is on.</returns>
    /// <exception cref="NotFoundException">There's no such user.</exception>
    Task<AdminChange> RemoveAdminAsync(Guid userId, CancellationToken cancellationToken);
}
