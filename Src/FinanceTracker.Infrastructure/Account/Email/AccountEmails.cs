using FinanceTracker.Emails;
using FinanceTracker.Emails.Templates;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.Notifications.Email;

namespace FinanceTracker.Infrastructure.Account.Email;

/// <inheritdoc cref="IAccountEmails"/>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Singleton<IAccountEmails>]
public sealed class AccountEmails : IAccountEmails
{
    /// <summary>
    /// The subject of the email confirming a new account's email.
    /// </summary>
    public const string ConfirmEmailSubject = "Confirm your email for FinTrack";

    /// <summary>
    /// The subject of the password reset email.
    /// </summary>
    public const string ResetPasswordSubject = "Reset your FinTrack password";

    /// <summary>
    /// The subject of the email confirming a new email.
    /// </summary>
    public const string ChangeEmailSubject = "Confirm your new email for FinTrack";

    /// <summary>
    /// The subject of the email saying two-factor sign in was turned on.
    /// </summary>
    public const string TwoFactorOnSubject = "Two-factor sign in is on for your FinTrack account";

    /// <summary>
    /// The subject of the email saying two-factor sign in was turned off.
    /// </summary>
    public const string TwoFactorOffSubject = "Two-factor sign in is off for your FinTrack account";

    /// <summary>
    /// The subject of the email saying new recovery codes were made.
    /// </summary>
    public const string RecoveryCodesMadeSubject = "New recovery codes for your FinTrack account";

    /// <summary>
    /// Where security notices link to, the profile's two-factor settings.
    /// </summary>
    private const string ProfilePath = "YourProfile";

    /// <summary>
    /// Why a security notice came, and what to do if the change wasn't the user's.
    /// </summary>
    private const string SecurityReason =
        "You're getting this because sign in changed on your FinTrack account. If that wasn't you, change your " +
        "password straight away and tell your FinTrack admin.";

    private readonly IEmailRenderer _emailRenderer;
    private readonly PublicAddress _publicAddress;

    /// <param name="emailRenderer">Renders the emails.</param>
    /// <param name="publicAddress">The app's address as people reach it.</param>
    public AccountEmails(IEmailRenderer emailRenderer, PublicAddress publicAddress)
    {
        _emailRenderer = emailRenderer;
        _publicAddress = publicAddress;
    }

    /// <inheritdoc/>
    public Task<MailMessage> ConfirmEmailAsync(string to, Uri link) =>
        CreateAsync(to, link, new AccountEmail(
            ConfirmEmailSubject,
            "Confirm your email",
            "Confirm this is your email so FinTrack can send you your notifications, and a link if you ever forget " +
            "your password.",
            "Confirm your email",
            "You're getting this because a FinTrack account was made with this email. If that wasn't you, you can " +
            "ignore it."));

    /// <inheritdoc/>
    public Task<MailMessage> ResetPasswordAsync(string to, Uri link) =>
        CreateAsync(to, link, new AccountEmail(
            ResetPasswordSubject,
            "Reset your password",
            "Choose a new password for your FinTrack account. The link works once.",
            "Choose a new password",
            "You're getting this because someone asked to reset your FinTrack password. If that wasn't you, you can " +
            "ignore it and your password stays the same."));

    /// <inheritdoc/>
    public Task<MailMessage> ChangeEmailAsync(string to, Uri link) =>
        CreateAsync(to, link, new AccountEmail(
            ChangeEmailSubject,
            "Confirm your new email",
            "Confirm this email to make it the one your FinTrack account uses. Until then, it keeps the old one.",
            "Use this email",
            "You're getting this because someone asked to move a FinTrack account to this email. If that wasn't " +
            "you, you can ignore it."));

    /// <inheritdoc/>
    public Task<MailMessage> TwoFactorOnAsync(string to) =>
        CreateAsync(to, _publicAddress.LinkTo(ProfilePath), new AccountEmail(
            TwoFactorOnSubject,
            "Two-factor sign in is on",
            "Signing in with your password now also asks for a code from your authenticator app. Keep your recovery " +
            "codes somewhere safe, they're the way in if you lose your phone.",
            "Open your profile",
            SecurityReason));

    /// <inheritdoc/>
    public Task<MailMessage> TwoFactorOffAsync(string to, TwoFactorOffCause cause) =>
        CreateAsync(to, _publicAddress.LinkTo(ProfilePath), new AccountEmail(
            TwoFactorOffSubject,
            "Two-factor sign in is off",
            TwoFactorOffMessage(cause),
            "Open your profile",
            SecurityReason));

    /// <inheritdoc/>
    public Task<MailMessage> RecoveryCodesMadeAsync(string to) =>
        CreateAsync(to, _publicAddress.LinkTo(ProfilePath), new AccountEmail(
            RecoveryCodesMadeSubject,
            "New recovery codes",
            "New recovery codes were made for your FinTrack account. The old ones no longer work.",
            "Open your profile",
            SecurityReason));

    /// <summary>
    /// What the email saying two-factor sign in is off says, by how it was turned off.
    /// </summary>
    /// <param name="cause">How it was turned off.</param>
    private static string TwoFactorOffMessage(TwoFactorOffCause cause)
    {
        switch (cause)
        {
            case TwoFactorOffCause.TurnedOff:
                return "Two-factor sign in was turned off, so signing in only asks for your password. You can turn " +
                       "it on again from your profile.";
            case TwoFactorOffCause.AuthenticatorReset:
                return "Your authenticator was reset, so two-factor sign in is off and your old app's codes no " +
                       "longer work. Set it up again from your profile.";
            case TwoFactorOffCause.TurnedOffByAdmin:
                return "Your FinTrack admin turned off two-factor sign in for you, so signing in only asks for your " +
                       "password. Set it up again from your profile.";
            default:
                throw new ArgumentOutOfRangeException(nameof(cause), cause, null);
        }
    }

    /// <summary>
    /// Renders an account email, with its link as the button when there is one.
    /// </summary>
    /// <param name="to">Who it's sent to.</param>
    /// <param name="link">The link the email is for, null for none when it's only a notice.</param>
    /// <param name="email">What the email says.</param>
    private async Task<MailMessage> CreateAsync(string to, Uri? link, AccountEmail email)
    {
        string html = await _emailRenderer.RenderAsync<AccountEmailBody>(new Dictionary<string, object?>
        {
            [nameof(AccountEmailBody.Title)] = email.Title,
            [nameof(AccountEmailBody.Message)] = email.Message,
            [nameof(AccountEmailBody.ButtonText)] = email.ButtonText,
            [nameof(AccountEmailBody.Link)] = link,
            [nameof(AccountEmailBody.Reason)] = email.Reason,
            [nameof(AccountEmailBody.PublicUrl)] = _publicAddress.Url
        });

        string text = $"{email.Message}\n\n{email.ButtonText}: {link}\n\n--\n{email.Reason}\n";
        if (link is null)
        {
            text = $"{email.Message}\n\n--\n{email.Reason}\n";
        }

        return new MailMessage(to, email.Subject, text, html) { Images = [EmailBrand.Logo] };
    }
}
