using FinanceTracker.Contracts.Profile;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Account.Email;
using FinanceTracker.Infrastructure.Notifications.Email;
using Microsoft.AspNetCore.Identity;

namespace FinanceTracker.Components.Account;

/// <summary>
/// <see cref="IProfileService"/> over Identity's <see cref="UserManager{TUser}"/>, for the signed in user.
/// </summary>
public sealed class ProfileService : IProfileService
{
    /// <summary>
    /// How many recovery codes are made at a time.
    /// </summary>
    private const int RecoveryCodeCount = 10;

    private readonly UserManager<FinanceTrackerUser> _userManager;
    private readonly SignInManager<FinanceTrackerUser> _signInManager;
    private readonly ICurrentUser _currentUser;
    private readonly IMailSender _mailSender;
    private readonly SignInMethods _signInMethods;
    private readonly IEmailConfirmation _emailConfirmation;
    private readonly IEmailChange _emailChange;
    private readonly ISecurityEmails _securityEmails;
    private readonly ILogger<ProfileService> _logger;

    /// <param name="userManager">Finds and changes users.</param>
    /// <param name="signInManager">Refreshes the user's sign in after a change.</param>
    /// <param name="currentUser">The signed in user.</param>
    /// <param name="mailSender">Sends the emails.</param>
    /// <param name="emailConfirmation">Sends the confirmation email.</param>
    /// <param name="emailChange">Starts an email change.</param>
    /// <param name="signInMethods">Whether passwords can be used.</param>
    /// <param name="securityEmails">Tells the user when how they sign in changes.</param>
    /// <param name="logger">Its logger.</param>
    public ProfileService(UserManager<FinanceTrackerUser> userManager,
        SignInManager<FinanceTrackerUser> signInManager, ICurrentUser currentUser, IMailSender mailSender,
        IEmailConfirmation emailConfirmation, IEmailChange emailChange, SignInMethods signInMethods,
        ISecurityEmails securityEmails, ILogger<ProfileService> logger)
    {
        _securityEmails = securityEmails;
        _signInMethods = signInMethods;
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
            HasPassword = await CanUsePasswordAsync(user),
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
        if (!await CanUsePasswordAsync(user))
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
        if (!await CanUsePasswordAsync(user))
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

    /// <inheritdoc/>
    public async Task<TwoFactorStatus?> GetTwoFactorAsync(CancellationToken cancellationToken)
    {
        FinanceTrackerUser user = await UserAsync();
        if (!await CanUsePasswordAsync(user))
        {
            return null;
        }

        return new TwoFactorStatus
        {
            Enabled = await _userManager.GetTwoFactorEnabledAsync(user),
            RecoveryCodesLeft = await _userManager.CountRecoveryCodesAsync(user),
            BrowserRemembered = await _signInManager.IsTwoFactorClientRememberedAsync(user)
        };
    }

    /// <inheritdoc/>
    public async Task<AuthenticatorSetup?> GetAuthenticatorSetupAsync(CancellationToken cancellationToken)
    {
        FinanceTrackerUser user = await UserAsync();
        if (!await CanUsePasswordAsync(user) || await _userManager.GetTwoFactorEnabledAsync(user))
        {
            return null;
        }

        string? key = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            // A new key changes the security stamp, this browser signs in again to stay signed in
            await _userManager.ResetAuthenticatorKeyAsync(user);
            await _signInManager.RefreshSignInAsync(user);
            key = await _userManager.GetAuthenticatorKeyAsync(user) ?? string.Empty;
        }

        return new AuthenticatorSetup
        {
            SharedKey = AuthenticatorQrCode.Grouped(key),
            QrCodeSvg = AuthenticatorQrCode.Svg(user.UserName ?? string.Empty, key)
        };
    }

    /// <inheritdoc/>
    public async Task<TwoFactorChange?> EnableAuthenticatorAsync(EnableAuthenticator request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        FinanceTrackerUser user = await UserAsync();
        if (!await CanUsePasswordAsync(user))
        {
            return null;
        }

        if (await PasswordRefusedAsync(user, request.Password) is { } refused)
        {
            return refused;
        }

        if (await _userManager.GetTwoFactorEnabledAsync(user))
        {
            return Refused("Two-factor sign in is already on");
        }

        bool rightCode = await _userManager.VerifyTwoFactorTokenAsync(user,
            _userManager.Options.Tokens.AuthenticatorTokenProvider, TwoFactorCodes.CleanInput(request.Code));
        if (!rightCode)
        {
            return Refused("That code isn't right, try the one your app shows now");
        }

        await _userManager.SetTwoFactorEnabledAsync(user, true);
        IEnumerable<string>? codes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);
        await _signInManager.RefreshSignInAsync(user);
        _logger.LogTwoFactorEnabled(user.Id);
        await _securityEmails.TwoFactorOnAsync(user);
        return new TwoFactorChange { RecoveryCodes = codes?.ToList() ?? [] };
    }

    /// <inheritdoc/>
    public async Task<TwoFactorChange?> MakeRecoveryCodesAsync(ConfirmPassword request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        FinanceTrackerUser user = await UserAsync();
        if (!await CanUsePasswordAsync(user))
        {
            return null;
        }

        if (await PasswordRefusedAsync(user, request.Password) is { } refused)
        {
            return refused;
        }

        if (!await _userManager.GetTwoFactorEnabledAsync(user))
        {
            return Refused("Turn on two-factor sign in first");
        }

        IEnumerable<string>? codes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);
        _logger.LogRecoveryCodesMade(user.Id);
        await _securityEmails.RecoveryCodesMadeAsync(user);
        return new TwoFactorChange { RecoveryCodes = codes?.ToList() ?? [] };
    }

    /// <inheritdoc/>
    public async Task<TwoFactorChange?> ResetAuthenticatorAsync(ConfirmPassword request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        FinanceTrackerUser user = await UserAsync();
        if (!await CanUsePasswordAsync(user))
        {
            return null;
        }

        if (await PasswordRefusedAsync(user, request.Password) is { } refused)
        {
            return refused;
        }

        // Off until they set the new key up, the old phone's codes stop working
        bool wasOn = await _userManager.GetTwoFactorEnabledAsync(user);
        await _userManager.SetTwoFactorEnabledAsync(user, false);
        await _userManager.ResetAuthenticatorKeyAsync(user);
        await _signInManager.RefreshSignInAsync(user);
        _logger.LogAuthenticatorReset(user.Id);
        if (wasOn)
        {
            await _securityEmails.TwoFactorOffAsync(user, TwoFactorOffCause.AuthenticatorReset);
        }

        return new TwoFactorChange();
    }

    /// <inheritdoc/>
    public async Task<TwoFactorChange?> TurnOffTwoFactorAsync(ConfirmPassword request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        FinanceTrackerUser user = await UserAsync();
        if (!await CanUsePasswordAsync(user))
        {
            return null;
        }

        if (await PasswordRefusedAsync(user, request.Password) is { } refused)
        {
            return refused;
        }

        bool wasOn = await _userManager.GetTwoFactorEnabledAsync(user);
        await _userManager.SetTwoFactorEnabledAsync(user, false);
        await _signInManager.RefreshSignInAsync(user);
        _logger.LogTwoFactorDisabled(user.Id);
        if (wasOn)
        {
            await _securityEmails.TwoFactorOffAsync(user, TwoFactorOffCause.TurnedOff);
        }

        return new TwoFactorChange();
    }

    /// <inheritdoc/>
    public Task ForgetBrowserAsync(CancellationToken cancellationToken) =>
        _signInManager.ForgetTwoFactorClientAsync();

    /// <summary>
    /// A refusal when <paramref name="password"/> isn't <paramref name="user"/>'s, null when it is.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="password">The password they typed.</param>
    private async Task<TwoFactorChange?> PasswordRefusedAsync(FinanceTrackerUser user, string password)
    {
        if (await _userManager.CheckPasswordAsync(user, password))
        {
            return null;
        }

        return Refused("Your password isn't right");
    }

    /// <summary>
    /// A change that wasn't made, saying why.
    /// </summary>
    /// <param name="error">Why, in words for the user.</param>
    private static TwoFactorChange Refused(string error) => new() { Errors = [error] };

    /// <summary>
    /// The signed in user.
    /// </summary>
    /// <exception cref="InvalidOperationException">They no longer exist.</exception>
    private async Task<FinanceTrackerUser> UserAsync() =>
        await _userManager.FindByIdAsync(_currentUser.UserId.ToString())
        ?? throw new InvalidOperationException("The signed in user no longer exists");

    /// <summary>
    /// Whether <paramref name="user"/> has a password they can use, see <see cref="SignInMethods.CanUsePassword"/>.
    /// </summary>
    /// <param name="user">The user.</param>
    private async Task<bool> CanUsePasswordAsync(FinanceTrackerUser user) =>
        _signInMethods.CanUsePassword(await _userManager.HasPasswordAsync(user));
}
