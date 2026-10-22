using FinanceTracker.Contracts.Auth;
using FinanceTracker.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Components.Account;

/// <summary>
/// The first user to register becomes the <see cref="FinTrackRoles.Admin"/>. A stopgap until user management lets
/// admins grant the role themselves.
/// </summary>
/// <remarks>
/// A user's role claims are read at sign in, so granting before the new user's first sign in puts the role in their
/// first cookie.
/// </remarks>
public sealed class AdminProvisioner
{
    private readonly ILogger<AdminProvisioner> _logger;
    private readonly RoleManager<FinanceTrackerRole> _roleManager;
    private readonly UserManager<FinanceTrackerUser> _userManager;

    public AdminProvisioner(UserManager<FinanceTrackerUser> userManager, RoleManager<FinanceTrackerRole> roleManager,
        ILogger<AdminProvisioner> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    /// <summary>
    /// Makes <paramref name="user"/> the admin when they're the only user, i.e. the first to register. Call right
    /// after creating a user.
    /// </summary>
    public async Task GrantIfFirstUserAsync(FinanceTrackerUser user)
    {
        if (await _userManager.Users.CountAsync() != 1)
        {
            return;
        }

        await GrantAsync(user);
    }

    /// <summary>
    /// Run at startup for installs whose users registered before the admin role existed: with nobody holding the
    /// role and a single user, that user is the first to have registered, so they become the admin.
    /// </summary>
    public async Task EnsureAdminAsync()
    {
        await EnsureRoleAsync();

        if ((await _userManager.GetUsersInRoleAsync(FinTrackRoles.Admin)).Count > 0)
        {
            return;
        }

        List<FinanceTrackerUser> users = await _userManager.Users.Take(2).ToListAsync();
        switch (users.Count)
        {
            case 1:
                await GrantAsync(users[0]);
                break;
            case > 1:
                // Users carry no registration time, so there's no telling which of them was first
                _logger.LogNoAdminAmongSeveralUsers();
                break;
        }
    }

    private async Task GrantAsync(FinanceTrackerUser user)
    {
        await EnsureRoleAsync();

        if (await _userManager.IsInRoleAsync(user, FinTrackRoles.Admin))
        {
            return;
        }

        ThrowIfFailed(await _userManager.AddToRoleAsync(user, FinTrackRoles.Admin));
        _logger.LogAdminGranted(user.Id);
    }

    private async Task EnsureRoleAsync()
    {
        if (!await _roleManager.RoleExistsAsync(FinTrackRoles.Admin))
        {
            ThrowIfFailed(await _roleManager.CreateAsync(new FinanceTrackerRole { Name = FinTrackRoles.Admin }));
        }
    }

    private static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Granting the admin role failed: {string.Join(", ", result.Errors.Select(static x => x.Description))}");
        }
    }
}
