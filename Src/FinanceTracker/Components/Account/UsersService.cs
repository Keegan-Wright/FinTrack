using System.Runtime.CompilerServices;
using FinanceTracker.Contracts;
using FinanceTracker.Contracts.Auth;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Users;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Account.Email;
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
    private readonly ISecurityEmails _securityEmails;
    private readonly ILogger<UsersService> _logger;
    private readonly SignInMethods _signInMethods;

    /// <param name="userManager">Finds and changes users.</param>
    /// <param name="passwordReset">Makes password reset links.</param>
    /// <param name="currentUser">The signed in user.</param>
    /// <param name="signInMethods">Whether passwords can be used.</param>
    /// <param name="securityEmails">Tells a user when how they sign in changes.</param>
    /// <param name="logger">Its logger.</param>
    public UsersService(UserManager<FinanceTrackerUser> userManager, IPasswordReset passwordReset,
        ICurrentUser currentUser, SignInMethods signInMethods, ISecurityEmails securityEmails,
        ILogger<UsersService> logger)
    {
        _securityEmails = securityEmails;
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
                Name = NameOf(user),
                Email = string.IsNullOrWhiteSpace(user.Email) ? null : user.Email,
                EmailConfirmed = user.EmailConfirmed,
                HasPassword = _signInMethods.CanUsePassword(user.PasswordHash is not null),
                TwoFactorEnabled = user.TwoFactorEnabled,
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
    public async Task<bool> TurnOffTwoFactorAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Their own goes through their profile, which asks for their password
        if (userId == _currentUser.UserId)
        {
            return false;
        }

        FinanceTrackerUser user = await FindAsync(userId);
        bool wasOn = await _userManager.GetTwoFactorEnabledAsync(user);

        ThrowIfFailed(await _userManager.SetTwoFactorEnabledAsync(user, false));

        // No recovery codes, so a lost list can't be used, and a new key, which also changes the security stamp:
        // they're signed out everywhere and no browser stays remembered
        if (await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 0) is null)
        {
            throw new InvalidOperationException($"User {user.Id}'s recovery codes couldn't be cleared");
        }

        ThrowIfFailed(await _userManager.ResetAuthenticatorKeyAsync(user));
        _logger.LogTwoFactorTurnedOffByAdmin(_currentUser.UserId, user.Id);

        if (wasOn)
        {
            await _securityEmails.TwoFactorOffAsync(user, TwoFactorOffCause.TurnedOffByAdmin);
        }

        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> LockAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Admins can't lock each other, or themselves, so there's always one who can unlock. Their role goes first
        FinanceTrackerUser user = await FindAsync(userId);
        if (await _userManager.IsInRoleAsync(user, FinTrackRoles.Admin))
        {
            return false;
        }

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

    /// <inheritdoc/>
    public async Task<AdminChange> GrantAdminAsync(Guid userId, CancellationToken cancellationToken)
    {
        FinanceTrackerUser user = await FindAsync(userId);

        // The identity provider's admin group decides for those who sign in through it
        if (user.OpenIdConnectSubject is not null)
        {
            return AdminChange.FromIdentityProvider;
        }

        if (await _userManager.IsInRoleAsync(user, FinTrackRoles.Admin))
        {
            return AdminChange.Changed;
        }

        // No new security stamp, which would sign them out. Their sign in is rebuilt with the role within a minute
        ThrowIfFailed(await _userManager.AddToRoleAsync(user, FinTrackRoles.Admin));
        _logger.LogAdminRoleGranted(_currentUser.UserId, user.Id);
        return AdminChange.Changed;
    }

    /// <inheritdoc/>
    public async Task<AdminChange> RemoveAdminAsync(Guid userId, CancellationToken cancellationToken)
    {
        FinanceTrackerUser user = await FindAsync(userId);

        if (user.OpenIdConnectSubject is not null)
        {
            return AdminChange.FromIdentityProvider;
        }

        if (!await _userManager.IsInRoleAsync(user, FinTrackRoles.Admin))
        {
            return AdminChange.Changed;
        }

        // With password sign in off the identity provider's admin group is the way back in, otherwise nobody could be
        if (_signInMethods.Password && (await _userManager.GetUsersInRoleAsync(FinTrackRoles.Admin)).Count == 1)
        {
            return AdminChange.LastAdmin;
        }

        ThrowIfFailed(await _userManager.RemoveFromRoleAsync(user, FinTrackRoles.Admin));
        _logger.LogAdminRoleRemoved(_currentUser.UserId, user.Id);
        return AdminChange.Changed;
    }

    /// <summary>
    /// <paramref name="user"/>'s first and last name, their username when they gave none.
    /// </summary>
    /// <param name="user">The user.</param>
    private static string NameOf(FinanceTrackerUser user)
    {
        string name = $"{user.FirstName} {user.LastName}".Trim();
        if (name.Length == 0)
        {
            return user.UserName ?? string.Empty;
        }

        return name;
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
