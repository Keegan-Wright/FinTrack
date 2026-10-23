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
    public const string Subject = "sub";
    public const string Email = "email";
    public const string EmailVerified = "email_verified";
    public const string GivenName = "given_name";
    public const string FamilyName = "family_name";
    public const string PreferredUsername = "preferred_username";

    private readonly AdminProvisioner _adminProvisioner;
    private readonly ILogger<OidcUserLinker> _logger;
    private readonly UserManager<FinanceTrackerUser> _userManager;

    public OidcUserLinker(UserManager<FinanceTrackerUser> userManager, AdminProvisioner adminProvisioner,
        ILogger<OidcUserLinker> logger)
    {
        _userManager = userManager;
        _adminProvisioner = adminProvisioner;
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

    private async Task<FinanceTrackerUser?> LinkAsync(FinanceTrackerUser user, string subject)
    {
        // Already linked to another identity at the provider, which happens to share the email
        if (user.OpenIdConnectSubject is not null)
        {
            _logger.LogOidcSignInRejected("the user with its email is linked to another subject");
            return null;
        }

        user.SetOidcSubject(subject);
        if (!Succeeded(await _userManager.UpdateAsync(user)))
        {
            return null;
        }

        _logger.LogOidcUserLinked(user.Id);
        return user;
    }

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

        FinanceTrackerUser user = new(firstName, lastName, email, userName, subject);
        if (!Succeeded(await _userManager.CreateAsync(user)))
        {
            return null;
        }

        await _userManager.AddClaimAsync(user, new Claim(CustomClaims.FirstName, firstName));
        await _adminProvisioner.GrantIfFirstUserAsync(user);
        _logger.LogOidcUserRegistered(user.Id);
        return user;
    }

    private bool Succeeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            _logger.LogOidcSignInRejected(string.Join(", ", result.Errors.Select(static x => x.Description)));
        }

        return result.Succeeded;
    }

    private static bool IsTrue(string? value) => bool.TryParse(value, out bool parsed) && parsed;
}
