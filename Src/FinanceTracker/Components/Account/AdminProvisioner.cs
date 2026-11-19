using FinanceTracker.Contracts.Auth;
using FinanceTracker.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Components.Account;

/// <summary>
/// Makes the first admin on an fresh database. The first user to register with a password becomes the
/// <see cref="FinTrackRoles.Admin"/>. After that admins give the role on the Users page, and the identity provider's
/// admins group decides for those who sign in through it.
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

    /// <param name="userManager">Finds and changes users.</param>
    /// <param name="roleManager">Finds and adds roles.</param>
    /// <param name="logger">Its logger.</param>
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
    /// <param name="user">The user just created.</param>
    public async Task GrantIfFirstUserAsync(FinanceTrackerUser user)
    {
        if (await _userManager.Users.CountAsync() != 1)
        {
            return;
        }

        await GrantAsync(user);
    }

    /// <summary>
    /// Run at startup. Adds the admin role when it doesn't exist. With nobody holding it and a single user, that user
    /// becomes the admin. With several users and no admin, warns how to get one, as nobody can manage users.
    /// </summary>
    public async Task EnsureAdminAsync()
    {
        await EnsureRoleAsync();

        if ((await _userManager.GetUsersInRoleAsync(FinTrackRoles.Admin)).Count > 0)
        {
            return;
        }

        List<FinanceTrackerUser> users = await _userManager.Users.Take(2).ToListAsync();
        if (users.Count == 1)
        {
            await GrantAsync(users[0]);
            return;
        }

        // An empty install gets its admin when the first person registers, nothing's wrong yet
        if (users.Count > 1)
        {
            _logger.LogNoAdminLeft();
        }
    }

    /// <summary>
    /// Makes <paramref name="user"/> an admin, adding the role first when it doesn't exist.
    /// </summary>
    /// <param name="user">The user.</param>
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

    /// <summary>
    /// Adds the admin role when it doesn't exist.
    /// </summary>
    private async Task EnsureRoleAsync()
    {
        if (!await _roleManager.RoleExistsAsync(FinTrackRoles.Admin))
        {
            ThrowIfFailed(await _roleManager.CreateAsync(new FinanceTrackerRole { Name = FinTrackRoles.Admin }));
        }
    }

    /// <summary>
    /// Throws when Identity refused the change.
    /// </summary>
    /// <param name="result">What Identity said.</param>
    /// <exception cref="InvalidOperationException">It refused.</exception>
    private static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Granting the admin role failed: {string.Join(", ", result.Errors.Select(static x => x.Description))}");
        }
    }
}
