using System.Text;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure.Notifications.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace FinanceTracker.Components.Account;

/// <summary>
/// Emails a user the link that confirms their email, see <c>ConfirmEmail.razor</c>. Notifications and password
/// resets are only emailed to a confirmed one.
/// </summary>
public sealed class EmailConfirmation
{
    /// <summary>
    /// The page the link opens, relative to the app.
    /// </summary>
    public const string PagePath = "Account/ConfirmEmail";

    private readonly UserManager<FinanceTrackerUser> _userManager;
    private readonly IEmailSender<FinanceTrackerUser> _emailSender;
    private readonly IMailSender _mailSender;
    private readonly AccountUrls _accountUrls;

    public EmailConfirmation(UserManager<FinanceTrackerUser> userManager,
        IEmailSender<FinanceTrackerUser> emailSender, IMailSender mailSender, AccountUrls accountUrls)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _mailSender = mailSender;
        _accountUrls = accountUrls;
    }

    /// <summary>
    /// Sends the link, unless there's nothing to confirm or no way to send it.
    /// </summary>
    /// <returns>Whether it was sent not when it's confirmed already, there's no email, or no mail server.</returns>
    /// <exception cref="Exception">Sending failed, see <see cref="IMailSender.SendAsync"/>.</exception>
    public async Task<bool> SendAsync(FinanceTrackerUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (!_mailSender.IsAvailable || string.IsNullOrWhiteSpace(user.Email)
                                     || await _userManager.IsEmailConfirmedAsync(user))
        {
            return false;
        }

        string code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        Uri link = _accountUrls.Create(PagePath, new Dictionary<string, string?>
        {
            ["userId"] = user.Id.ToString(),
            ["code"] = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code))
        });

        await _emailSender.SendConfirmationLinkAsync(user, user.Email, link.AbsoluteUri);
        return true;
    }
}
