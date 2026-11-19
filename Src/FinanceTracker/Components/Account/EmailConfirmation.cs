using System.Text;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure.Account.Email;
using FinanceTracker.Infrastructure.Notifications.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace FinanceTracker.Components.Account;

/// <summary>
/// Emails a user the link that confirms their email, see <c>ConfirmEmail.razor</c>. Notifications and password
/// resets are only emailed to a confirmed one.
/// </summary>
public sealed class EmailConfirmation : IEmailConfirmation
{
    /// <summary>
    /// The page the link opens, relative to the app.
    /// </summary>
    public const string PagePath = "Account/ConfirmEmail";

    private readonly UserManager<FinanceTrackerUser> _userManager;
    private readonly IAccountEmails _accountEmails;
    private readonly IMailSender _mailSender;
    private readonly IAccountUrls _accountUrls;

    /// <param name="userManager">Finds and changes users.</param>
    /// <param name="accountEmails">Writes the email.</param>
    /// <param name="mailSender">Sends the emails.</param>
    /// <param name="accountUrls">Builds the links in the emails.</param>
    public EmailConfirmation(UserManager<FinanceTrackerUser> userManager, IAccountEmails accountEmails,
        IMailSender mailSender, IAccountUrls accountUrls)
    {
        _userManager = userManager;
        _accountEmails = accountEmails;
        _mailSender = mailSender;
        _accountUrls = accountUrls;
    }

    /// <inheritdoc/>
    public async Task<bool> SendAsync(FinanceTrackerUser user, CancellationToken cancellationToken)
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

        // Straight to the mail server rather than through Identity's email sender, which can't be cancelled
        await _mailSender.SendAsync(await _accountEmails.ConfirmEmailAsync(user.Email, link), cancellationToken);
        return true;
    }
}
