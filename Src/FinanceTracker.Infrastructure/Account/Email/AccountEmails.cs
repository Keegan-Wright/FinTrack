using FinanceTracker.Emails;
using FinanceTracker.Emails.Templates;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.Notifications.Email;

namespace FinanceTracker.Infrastructure.Account.Email;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Singleton<IAccountEmails>]
public sealed class AccountEmails : IAccountEmails
{
    public const string ConfirmEmailSubject = "Confirm your email for FinTrack";
    public const string ResetPasswordSubject = "Reset your FinTrack password";
    public const string ChangeEmailSubject = "Confirm your new email for FinTrack";

    private readonly IEmailRenderer _renderer;
    private readonly PublicAddress _publicAddress;

    public AccountEmails(IEmailRenderer renderer, PublicAddress publicAddress)
    {
        _renderer = renderer;
        _publicAddress = publicAddress;
    }

    public Task<MailMessage> ConfirmEmailAsync(string to, Uri link) =>
        CreateAsync(to, link, new AccountEmail(
            ConfirmEmailSubject,
            "Confirm your email",
            "Confirm this is your email so FinTrack can send you your notifications, and a link if you ever forget " +
            "your password.",
            "Confirm your email",
            "You're getting this because a FinTrack account was made with this email. If that wasn't you, you can " +
            "ignore it."));

    public Task<MailMessage> ResetPasswordAsync(string to, Uri link) =>
        CreateAsync(to, link, new AccountEmail(
            ResetPasswordSubject,
            "Reset your password",
            "Choose a new password for your FinTrack account. The link works once.",
            "Choose a new password",
            "You're getting this because someone asked to reset your FinTrack password. If that wasn't you, you can " +
            "ignore it and your password stays the same."));

    public Task<MailMessage> ChangeEmailAsync(string to, Uri link) =>
        CreateAsync(to, link, new AccountEmail(
            ChangeEmailSubject,
            "Confirm your new email",
            "Confirm this email to make it the one your FinTrack account uses. Until then, it keeps the old one.",
            "Use this email",
            "You're getting this because someone asked to move a FinTrack account to this email. If that wasn't " +
            "you, you can ignore it."));

    private async Task<MailMessage> CreateAsync(string to, Uri link, AccountEmail email)
    {
        ArgumentNullException.ThrowIfNull(link);

        string html = await _renderer.RenderAsync<AccountEmailBody>(new Dictionary<string, object?>
        {
            [nameof(AccountEmailBody.Title)] = email.Title,
            [nameof(AccountEmailBody.Message)] = email.Message,
            [nameof(AccountEmailBody.ButtonText)] = email.ButtonText,
            [nameof(AccountEmailBody.Link)] = link,
            [nameof(AccountEmailBody.Reason)] = email.Reason,
            [nameof(AccountEmailBody.PublicUrl)] = _publicAddress.Url
        });

        string text = $"{email.Message}\n\n{email.ButtonText}: {link}\n\n--\n{email.Reason}\n";

        return new MailMessage(to, email.Subject, text, html) { Images = [EmailBrand.Logo] };
    }
}
