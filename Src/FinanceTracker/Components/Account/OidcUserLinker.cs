using System.Security.Claims;
using FinanceTracker.Contracts.Auth;
using FinanceTracker.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Components.Account;

/// <summary>
/// Finds the local user an OIDC sign in belongs to the one already linked to its subject, else the one with its
/// verified email (linking them), else a new user.
/// </summary>
/// <remarks>
/// Reads the provider's claims by their OIDC names, the handler is set not to map them.
/// </remarks>
public sealed class OidcUserLinker
{
    /// <summary>
    /// The claim with the user's id at the provider.
    /// </summary>
    public const string Subject = "sub";
    /// <summary>
    /// The claim with the user's email.
    /// </summary>
    public const string Email = "email";
    /// <summary>
    /// The claim saying whether the provider has verified the email.
    /// </summary>
    public const string EmailVerified = "email_verified";
    /// <summary>
    /// The claim with the user's first name.
    /// </summary>
    public const string GivenName = "given_name";
    /// <summary>
    /// The claim with the user's last name.
    /// </summary>
    public const string FamilyName = "family_name";
    /// <summary>
    /// The claim with the username the user goes by.
    /// </summary>
    public const string PreferredUsername = "preferred_username";
    /// <summary>
    /// The claim with each group the user is in at the provider, one claim per group, and the scope that asks for
    /// them.
    /// </summary>
    public const string Groups = "groups";
    /// <summary>
    /// The provider's group whose members are FinTrack's admins.
    /// </summary>
    public const string AdminsGroup = "admins";

    private readonly ILogger<OidcUserLinker> _logger;
    private readonly UserManager<FinanceTrackerUser> _userManager;

    /// <param name="userManager">Finds and changes users.</param>
    /// <param name="logger">Its logger.</param>
    public OidcUserLinker(UserManager<FinanceTrackerUser> userManager, ILogger<OidcUserLinker> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// The user to sign in for <paramref name="external"/>, or <see langword="null"/> when there's none to sign in,
    /// the reason is logged.
    /// </summary>
    /// <param name="external">The principal the OIDC handler signed into the external cookie.</param>
    public async Task<FinanceTrackerUser?> FindOrCreateAsync(ClaimsPrincipal external)
    {
        string? subject = external.FindFirstValue(Subject);
        if (string.IsNullOrEmpty(subject))
        {
            _logger.LogOidcSignInRejected("the provider sent no subject");
            return null;
        }

        FinanceTrackerUser? user = await _userManager.Users.FirstOrDefaultAsync(x => x.OpenIdConnectSubject == subject);
        if (user is not null)
        {
            await ConfirmIfVerifiedAsync(user, external);
            return user;
        }

        // Users are matched and created by email, an unverified one could belong to someone else
        string? email = external.FindFirstValue(Email);
        if (string.IsNullOrEmpty(email) || !IsTrue(external.FindFirstValue(EmailVerified)))
        {
            _logger.LogOidcSignInRejected("the provider sent no verified email");
            return null;
        }

        user = await _userManager.FindByEmailAsync(email);
        return user is null ? await CreateAsync(external, subject, email) : await LinkAsync(user, subject);
    }

    /// <summary>
    /// Links <paramref name="user"/>, found by their verified email, to <paramref name="subject"/>. Refused when
    /// they're already linked to another.
    /// </summary>
    /// <param name="user">The user with the email.</param>
    /// <param name="subject">Their id at the provider.</param>
    /// <returns>The user, null when refused.</returns>
    private async Task<FinanceTrackerUser?> LinkAsync(FinanceTrackerUser user, string subject)
    {
        // Already linked to another identity at the provider, which happens to share the email
        if (user.OpenIdConnectSubject is not null)
        {
            _logger.LogOidcSignInRejected("the user with its email is linked to another subject");
            return null;
        }

        // The provider checked this email is theirs, which is how they matched
        user.SetOidcSubject(subject);
        user.EmailConfirmed = true;
        if (!Succeeded(await _userManager.UpdateAsync(user)))
        {
            return null;
        }

        _logger.LogOidcUserLinked(user.Id);
        return user;
    }

    /// <summary>
    /// Adds a user from the provider's claims, with their email confirmed.
    /// </summary>
    /// <param name="external">The provider's claims.</param>
    /// <param name="subject">Their id at the provider.</param>
    /// <param name="email">Their verified email.</param>
    /// <returns>The user, null when Identity refused them.</returns>
    private async Task<FinanceTrackerUser?> CreateAsync(ClaimsPrincipal external, string subject, string email)
    {
        string firstName = external.FindFirstValue(GivenName) ?? string.Empty;
        string lastName = external.FindFirstValue(FamilyName) ?? string.Empty;

        // Their username at the provider when nobody here has it, else the email, which is unique
        string? preferredUsername = external.FindFirstValue(PreferredUsername);
        string userName = !string.IsNullOrEmpty(preferredUsername) &&
                          await _userManager.FindByNameAsync(preferredUsername) is null
            ? preferredUsername
            : email;

        // Confirmed, as the provider checked the email is theirs
        FinanceTrackerUser user = new(firstName, lastName, email, userName, subject) { EmailConfirmed = true };
        if (!Succeeded(await _userManager.CreateAsync(user)))
        {
            return null;
        }

        // Not the first-user rule, the provider's admins group decides, see MatchAdminRoleAsync
        await _userManager.AddClaimAsync(user, new Claim(CustomClaims.FirstName, firstName));
        _logger.LogOidcUserRegistered(user.Id);
        return user;
    }

    /// <summary>
    /// Makes <paramref name="user"/> an admin when the provider has them in its <see cref="AdminsGroup"/>, and takes
    /// the role away when it doesn't, at every sign in. The provider is the source of truth for whoever signs in
    /// through it, even when that leaves nobody an admin, which is logged.
    /// </summary>
    /// <param name="user">The user signing in.</param>
    /// <param name="external">The provider's claims.</param>
    public async Task MatchAdminRoleAsync(FinanceTrackerUser user, ClaimsPrincipal external)
    {
        bool inGroup = external.FindAll(Groups).Any(static x => x.Value == AdminsGroup);
        bool isAdmin = await _userManager.IsInRoleAsync(user, FinTrackRoles.Admin);

        if (inGroup)
        {
            await GrantAdminAsync(user, isAdmin);
            return;
        }

        if (!isAdmin)
        {
            return;
        }

        if (!Succeeded(await _userManager.RemoveFromRoleAsync(user, FinTrackRoles.Admin)))
        {
            return;
        }

        _logger.LogAdminFromGroupRemoved(user.Id);
        if ((await _userManager.GetUsersInRoleAsync(FinTrackRoles.Admin)).Count == 0)
        {
            _logger.LogNoAdminLeft();
        }
    }

    /// <summary>
    /// Makes <paramref name="user"/>, in the provider's admins group, an admin unless they are already.
    /// </summary>
    /// <param name="user">The user signing in.</param>
    /// <param name="isAdmin">Whether they're an admin already.</param>
    private async Task GrantAdminAsync(FinanceTrackerUser user, bool isAdmin)
    {
        if (isAdmin)
        {
            return;
        }

        if (Succeeded(await _userManager.AddToRoleAsync(user, FinTrackRoles.Admin)))
        {
            _logger.LogAdminFromGroupGranted(user.Id);
        }
    }

    /// <summary>
    /// Confirms an already linked user's email when the provider says it checked that same email, e.g. for users
    /// linked before emails were confirmed. A failure only leaves it unconfirmed, sign in carries on.
    /// </summary>
    /// <param name="user">The linked user.</param>
    /// <param name="external">The provider's claims.</param>
    private async Task ConfirmIfVerifiedAsync(FinanceTrackerUser user, ClaimsPrincipal external)
    {
        string? email = external.FindFirstValue(Email);
        if (user.EmailConfirmed || !IsTrue(external.FindFirstValue(EmailVerified))
                                || !string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        user.EmailConfirmed = true;
        Succeeded(await _userManager.UpdateAsync(user));
    }

    /// <summary>
    /// Whether Identity took the change. Logs why when it didn't.
    /// </summary>
    /// <param name="result">What Identity said.</param>
    private bool Succeeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            _logger.LogOidcSignInRejected(string.Join(", ", result.Errors.Select(static x => x.Description)));
        }

        return result.Succeeded;
    }

    /// <summary>
    /// Whether a claim's value is <c>true</c>.
    /// </summary>
    /// <param name="value">The claim's value.</param>
    private static bool IsTrue(string? value) => bool.TryParse(value, out bool parsed) && parsed;
}
