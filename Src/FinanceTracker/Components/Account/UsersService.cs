using System.Runtime.CompilerServices;
using FinanceTracker.Contracts;
using FinanceTracker.Contracts.Auth;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Users;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Components.Account;

/// <summary>
/// <see cref="IUsersService"/> over Identity's <see cref="UserManager{TUser}"/>. Only admins reach it, the API's
/// <c>api/users</c> needs the role.
/// </summary>
public sealed class UsersService : IUsersService
{
    private readonly UserManager<FinanceTrackerUser> _userManager;
    private readonly IPasswordReset _passwordReset;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<UsersService> _logger;
    private readonly SignInMethods _signInMethods;

    /// <param name="userManager">Finds and changes users.</param>
    /// <param name="passwordReset">Makes password reset links.</param>
    /// <param name="currentUser">The signed in user.</param>
    /// <param name="signInMethods">Whether passwords can be used.</param>
    /// <param name="logger">Its logger.</param>
    public UsersService(UserManager<FinanceTrackerUser> userManager, IPasswordReset passwordReset,
        ICurrentUser currentUser, SignInMethods signInMethods, ILogger<UsersService> logger)
    {
        _signInMethods = signInMethods;
        _userManager = userManager;
        _passwordReset = passwordReset;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<UserSummary> GetUsersAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        HashSet<Guid> admins = (await _userManager.GetUsersInRoleAsync(FinTrackRoles.Admin))
            .Select(static x => x.Id)
            .ToHashSet();

        List<FinanceTrackerUser> users = await _userManager.Users.AsNoTracking()
            .OrderBy(static x => x.UserName)
            .ToListAsync(cancellationToken);

        foreach (FinanceTrackerUser user in users)
        {
            yield return new UserSummary
            {
                Id = user.Id,
                Username = user.UserName ?? string.Empty,
                Email = string.IsNullOrWhiteSpace(user.Email) ? null : user.Email,
                EmailConfirmed = user.EmailConfirmed,
                HasPassword = _signInMethods.CanUsePassword(user.PasswordHash is not null),
                UsesIdentityProvider = user.OpenIdConnectSubject is not null,
                IsAdmin = admins.Contains(user.Id),
                IsLocked = user.IsLockedByAdmin,
                IsYou = user.Id == _currentUser.UserId
            };
        }
    }

    /// <inheritdoc/>
    public async Task<ResetLink?> CreateResetLinkAsync(Guid userId, CancellationToken cancellationToken)
    {
        FinanceTrackerUser user = await FindAsync(userId);

        // Their password is the identity provider's, a reset would give them a way in around it. Or passwords are off
        if (!_signInMethods.CanUsePassword(await _userManager.HasPasswordAsync(user)))
        {
            return null;
        }

        Uri link = await _passwordReset.CreateLinkAsync(user);
        _logger.LogResetLinkCreated(_currentUser.UserId, user.Id);
        return new ResetLink { Url = link.AbsoluteUri, ExpiresAt = _passwordReset.ExpiresAt };
    }

    /// <inheritdoc/>
    public async Task<bool> LockAsync(Guid userId, CancellationToken cancellationToken)
    {
        // The admin locking would lose the page to unlock themselves with, and admins are never all locked
        if (userId == _currentUser.UserId)
        {
            return false;
        }

        FinanceTrackerUser user = await FindAsync(userId);
        user.LockByAdmin();

        // A new stamp saves the lock with it and signs them out, their cookie is turned away the next time it's checked
        ThrowIfFailed(await _userManager.UpdateSecurityStampAsync(user));
        _logger.LogUserLocked(_currentUser.UserId, user.Id);
        return true;
    }

    /// <inheritdoc/>
    public async Task UnlockAsync(Guid userId, CancellationToken cancellationToken)
    {
        FinanceTrackerUser user = await FindAsync(userId);
        user.Unlock();

        ThrowIfFailed(await _userManager.UpdateAsync(user));
        _logger.LogUserUnlocked(_currentUser.UserId, user.Id);
    }

    /// <summary>
    /// The user with <paramref name="userId"/>.
    /// </summary>
    /// <param name="userId">The user's id.</param>
    /// <exception cref="NotFoundException">There's no such user.</exception>
    private async Task<FinanceTrackerUser> FindAsync(Guid userId) =>
        await _userManager.FindByIdAsync(userId.ToString())
        ?? throw new NotFoundException($"There's no user {userId}");

    /// <summary>
    /// Throws when Identity refused the change.
    /// </summary>
    /// <param name="result">What Identity said.</param>
    /// <exception cref="AccountUpdateFailedException">It refused.</exception>
    private static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new AccountUpdateFailedException(result);
        }
    }
}
