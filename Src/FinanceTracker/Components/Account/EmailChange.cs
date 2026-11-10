using System.Text;
using FinanceTracker.Contracts.Profile;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure.Account.Email;
using FinanceTracker.Infrastructure.Notifications.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace FinanceTracker.Components.Account;

/// <summary>
/// Moves a user to a new email. With a mail server, by a link sent to the new one, see <c>ConfirmEmailChange.razor</c>,
/// so it's only used once it's shown to be theirs. The account keeps the old one until then. Without one, straight
/// away and unconfirmed, as nothing could confirm it.
/// </summary>
public sealed class EmailChange
{
    /// <summary>
    /// The page the link opens, relative to the app.
    /// </summary>
    public const string PagePath = "Account/ConfirmEmailChange";

    private readonly UserManager<FinanceTrackerUser> _userManager;
    private readonly IMailSender _mailSender;
    private readonly IAccountEmails _emails;
    private readonly AccountUrls _accountUrls;

    public EmailChange(UserManager<FinanceTrackerUser> userManager, IMailSender mailSender, IAccountEmails emails,
        AccountUrls accountUrls)
    {
        _userManager = userManager;
        _mailSender = mailSender;
        _emails = emails;
        _accountUrls = accountUrls;
    }

    /// <exception cref="Exception">Sending the link failed, see <see cref="IMailSender.SendAsync"/>.</exception>
    public async Task<ProfileChange> RequestAsync(FinanceTrackerUser user, string newEmail)
    {
        ArgumentNullException.ThrowIfNull(user);
        string email = newEmail.Trim();

        if (string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            return Failed("That's already your email");
        }

        if (await _userManager.FindByEmailAsync(email) is not null)
        {
            return Failed("Another account uses that email");
        }

        if (!_mailSender.IsAvailable)
        {
            return ProfileChanges.From(await _userManager.SetEmailAsync(user, email));
        }

        string code = await _userManager.GenerateChangeEmailTokenAsync(user, email);
        Uri link = _accountUrls.Create(PagePath, new Dictionary<string, string?>
        {
            ["userId"] = user.Id.ToString(),
            ["email"] = email,
            ["code"] = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code))
        });

        await _mailSender.SendAsync(await _emails.ChangeEmailAsync(email, link), CancellationToken.None);
        return new ProfileChange { LinkSent = true };
    }

    private static ProfileChange Failed(string error) => new() { Errors = [error] };
}
