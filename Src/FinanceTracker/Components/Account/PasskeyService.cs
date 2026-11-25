using FinanceTracker.Contracts.Profile;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace FinanceTracker.Components.Account;

/// <summary>
/// <see cref="IPasskeyService"/> over Identity's passkeys, for the signed in user. Adding one takes two requests, as
/// Identity keeps what it asked the browser for in a cookie between them.
/// </summary>
public sealed class PasskeyService : IPasskeyService
{
    private readonly UserManager<FinanceTrackerUser> _userManager;
    private readonly SignInManager<FinanceTrackerUser> _signInManager;
    private readonly ICurrentUser _currentUser;
    private readonly SignInMethods _signInMethods;
    private readonly ISecurityEmails _securityEmails;
    private readonly ILogger<PasskeyService> _logger;

    /// <param name="userManager">Finds users and keeps their passkeys.</param>
    /// <param name="signInManager">Asks the browser for a passkey and checks what it gives back.</param>
    /// <param name="currentUser">The signed in user.</param>
    /// <param name="signInMethods">Whether passwords can be used.</param>
    /// <param name="securityEmails">Tells the user when their passkeys change.</param>
    /// <param name="logger">Its logger.</param>
    public PasskeyService(UserManager<FinanceTrackerUser> userManager,
        SignInManager<FinanceTrackerUser> signInManager, ICurrentUser currentUser, SignInMethods signInMethods,
        ISecurityEmails securityEmails, ILogger<PasskeyService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _currentUser = currentUser;
        _signInMethods = signInMethods;
        _securityEmails = securityEmails;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PasskeySummary>?> GetPasskeysAsync(CancellationToken cancellationToken)
    {
        FinanceTrackerUser user = await UserAsync();
        if (!await CanUsePasskeysAsync(user))
        {
            return null;
        }

        IList<UserPasskeyInfo> passkeys = await _userManager.GetPasskeysAsync(user);
        return passkeys
            .OrderByDescending(static x => x.CreatedAt)
            .Select(static x => new PasskeySummary
            {
                Id = WebEncoders.Base64UrlEncode(x.CredentialId),
                Name = x.Name ?? "Passkey",
                AddedAt = x.CreatedAt,
                Synced = x.IsBackedUp
            })
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<PasskeyChange?> StartAddingAsync(ConfirmPassword request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        FinanceTrackerUser user = await UserAsync();
        if (!await CanUsePasskeysAsync(user))
        {
            return null;
        }

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return Refused("Your password isn't right");
        }

        PasskeyUserEntity entity = new()
        {
            Id = user.Id.ToString(),
            Name = user.UserName ?? string.Empty,
            DisplayName = DisplayNameOf(user)
        };
        string options = await _signInManager.MakePasskeyCreationOptionsAsync(entity);
        return new PasskeyAddingStarted(options);
    }

    /// <inheritdoc/>
    public async Task<PasskeyChange?> AddAsync(AddPasskey request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        FinanceTrackerUser user = await UserAsync();
        if (!await CanUsePasskeysAsync(user))
        {
            return null;
        }

        PasskeyAttestationResult result;
        try
        {
            result = await _signInManager.PerformPasskeyAttestationAsync(request.CredentialJson);
        }
        catch (InvalidOperationException)
        {
            // Nothing was started, or it ran out, as the password step's cookie only lasts the one attempt
            return Refused("That took too long, start adding the passkey again");
        }

        if (!result.Succeeded)
        {
            _logger.LogPasskeyNotAdded(user.Id, result.Failure.Message);
            return Refused("That passkey couldn't be added, try again");
        }

        // The password step was for this user, so the passkey must be too
        if (result.UserEntity.Id != user.Id.ToString())
        {
            _logger.LogPasskeyNotAdded(user.Id, "it was started for another user");
            return Refused("That passkey couldn't be added, try again");
        }

        result.Passkey.Name = request.Name.Trim();
        IdentityResult saved = await _userManager.AddOrUpdatePasskeyAsync(user, result.Passkey);
        if (!saved.Succeeded)
        {
            return Refused("That passkey couldn't be saved, try again");
        }

        _logger.LogPasskeyAdded(user.Id);
        await _securityEmails.PasskeyAddedAsync(user, result.Passkey.Name);
        return new PasskeyChangeMade();
    }

    /// <inheritdoc/>
    public async Task<PasskeyChange?> RenameAsync(string id, RenamePasskey request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        FinanceTrackerUser user = await UserAsync();
        if (!await CanUsePasskeysAsync(user))
        {
            return null;
        }

        UserPasskeyInfo? passkey = await FindAsync(user, id);
        if (passkey is null)
        {
            return Refused("That passkey has gone, it may have been removed");
        }

        passkey.Name = request.Name.Trim();
        IdentityResult saved = await _userManager.AddOrUpdatePasskeyAsync(user, passkey);
        if (!saved.Succeeded)
        {
            return Refused("That passkey couldn't be renamed, try again");
        }

        return new PasskeyChangeMade();
    }

    /// <inheritdoc/>
    public async Task<PasskeyChange?> RemoveAsync(string id, ConfirmPassword request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        FinanceTrackerUser user = await UserAsync();
        if (!await CanUsePasskeysAsync(user))
        {
            return null;
        }

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return Refused("Your password isn't right");
        }

        UserPasskeyInfo? passkey = await FindAsync(user, id);
        if (passkey is null)
        {
            return Refused("That passkey has gone, it may have been removed");
        }

        IdentityResult removed = await _userManager.RemovePasskeyAsync(user, passkey.CredentialId);
        if (!removed.Succeeded)
        {
            return Refused("That passkey couldn't be removed, try again");
        }

        _logger.LogPasskeyRemoved(user.Id);
        await _securityEmails.PasskeyRemovedAsync(user, passkey.Name ?? "Passkey");
        return new PasskeyChangeMade();
    }

    /// <summary>
    /// <paramref name="user"/>'s passkey with <paramref name="id"/>, null when there's none or the id isn't one.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="id">The passkey's id, base64url.</param>
    private async Task<UserPasskeyInfo?> FindAsync(FinanceTrackerUser user, string id)
    {
        byte[] credentialId;
        try
        {
            credentialId = WebEncoders.Base64UrlDecode(id);
        }
        catch (FormatException)
        {
            return null;
        }

        return await _userManager.GetPasskeyAsync(user, credentialId);
    }

    /// <summary>
    /// The name the browser shows for <paramref name="user"/>'s passkeys, their full name or their username.
    /// </summary>
    /// <param name="user">The user.</param>
    private static string DisplayNameOf(FinanceTrackerUser user)
    {
        string name = $"{user.FirstName} {user.LastName}".Trim();
        if (name.Length == 0)
        {
            return user.UserName ?? string.Empty;
        }

        return name;
    }

    /// <summary>
    /// A change that wasn't made, saying why.
    /// </summary>
    /// <param name="error">Why, in words for the user.</param>
    private static PasskeyChangeRefused Refused(string error) => new([error]);

    /// <summary>
    /// The signed in user.
    /// </summary>
    /// <exception cref="InvalidOperationException">They no longer exist.</exception>
    private async Task<FinanceTrackerUser> UserAsync() =>
        await _userManager.FindByIdAsync(_currentUser.UserId.ToString())
        ?? throw new InvalidOperationException("The signed in user no longer exists");

    /// <summary>
    /// Whether <paramref name="user"/> can have passkeys. Only with a password they can use, see
    /// <see cref="SignInMethods.CanUsePassword"/>, as one would let a single sign-on user get round the provider.
    /// </summary>
    /// <param name="user">The user.</param>
    private async Task<bool> CanUsePasskeysAsync(FinanceTrackerUser user) =>
        _signInMethods.CanUsePassword(await _userManager.HasPasswordAsync(user));
}
