using FinanceTracker.Contracts.Profile;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Notifications.Email;
using Microsoft.AspNetCore.Identity;

namespace FinanceTracker.Components.Account;

/// <summary>
/// <see cref="IProfileService"/> over Identity's <see cref="UserManager{TUser}"/>, for the signed in user.
/// </summary>
public sealed class ProfileService : IProfileService
{
    private readonly UserManager<FinanceTrackerUser> _userManager;
    private readonly SignInManager<FinanceTrackerUser> _signInManager;
    private readonly ICurrentUser _currentUser;
    private readonly IMailSender _mailSender;
    private readonly IEmailConfirmation _emailConfirmation;
    private readonly IEmailChange _emailChange;
    private readonly ILogger<ProfileService> _logger;

    /// <param name="userManager">Finds and changes users.</param>
    /// <param name="signInManager">Refreshes the user's sign in after a change.</param>
    /// <param name="currentUser">The signed in user.</param>
    /// <param name="mailSender">Sends the emails.</param>
    /// <param name="emailConfirmation">Sends the confirmation email.</param>
    /// <param name="emailChange">Starts an email change.</param>
    /// <param name="logger">Its logger.</param>
    public ProfileService(UserManager<FinanceTrackerUser> userManager,
        SignInManager<FinanceTrackerUser> signInManager, ICurrentUser currentUser, IMailSender mailSender,
        IEmailConfirmation emailConfirmation, IEmailChange emailChange, ILogger<ProfileService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _currentUser = currentUser;
        _mailSender = mailSender;
        _emailConfirmation = emailConfirmation;
        _emailChange = emailChange;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ProfileDetails> GetProfileAsync(CancellationToken cancellationToken)
    {
        FinanceTrackerUser user = await UserAsync();
        string? email = string.IsNullOrWhiteSpace(user.Email) ? null : user.Email;
        return new ProfileDetails
        {
            Username = user.UserName ?? string.Empty,
            Email = email,
            EmailConfirmed = user.EmailConfirmed,
            HasPassword = await _userManager.HasPasswordAsync(user),
            UsesIdentityProvider = user.OpenIdConnectSubject is not null,
            MailAvailable = _mailSender.IsAvailable
        };
    }

    /// <inheritdoc/>
    public async Task<bool> SendEmailConfirmationAsync(CancellationToken cancellationToken) =>
        await _emailConfirmation.SendAsync(await UserAsync());

    /// <inheritdoc/>
    public async Task<ProfileChange?> ChangePasswordAsync(ChangePassword change, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        FinanceTrackerUser user = await UserAsync();
        if (!await _userManager.HasPasswordAsync(user))
        {
            return null;
        }

        IdentityResult result = await _userManager.ChangePasswordAsync(user, change.CurrentPassword,
            change.NewPassword);
        if (result.Succeeded)
        {
            // Changing it signs out everywhere else, this browser signs in again to stay signed in
            await _signInManager.RefreshSignInAsync(user);
            _logger.LogPasswordChanged(user.Id);
        }

        return ProfileChanges.From(result);
    }

    /// <inheritdoc/>
    public async Task<ProfileChange?> ChangeEmailAsync(ChangeEmail change, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        FinanceTrackerUser user = await UserAsync();
        if (!await _userManager.HasPasswordAsync(user))
        {
            return null;
        }

        ProfileChange result = await _emailChange.RequestAsync(user, change.NewEmail);
        if (result is { Succeeded: true, LinkSent: false })
        {
            // Changed straight away, which signs out everywhere else as a password change does
            await _signInManager.RefreshSignInAsync(user);
        }

        return result;
    }

    /// <summary>
    /// The signed in user.
    /// </summary>
    /// <exception cref="InvalidOperationException">They no longer exist.</exception>
    private async Task<FinanceTrackerUser> UserAsync() =>
        await _userManager.FindByIdAsync(_currentUser.UserId.ToString())
        ?? throw new InvalidOperationException("The signed in user no longer exists");
}
