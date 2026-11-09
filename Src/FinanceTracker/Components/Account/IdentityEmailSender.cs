using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure.Account.Email;
using FinanceTracker.Infrastructure.Notifications.Email;
using Microsoft.AspNetCore.Identity;

namespace FinanceTracker.Components.Account;

/// <summary>
/// Sends Identity's account emails through <see cref="IMailSender"/>, see <see cref="IAccountEmails"/>. Without a
/// mail server nothing is sent, and the pages that need email say so instead of offering it. A failure to send
/// throws, for the page to decide what the user is told.
/// </summary>
public sealed class IdentityEmailSender : IEmailSender<FinanceTrackerUser>
{
    private readonly IMailSender _mailSender;
    private readonly IAccountEmails _emails;
    private readonly ILogger<IdentityEmailSender> _logger;

    public IdentityEmailSender(IMailSender mailSender, IAccountEmails emails, ILogger<IdentityEmailSender> logger)
    {
        _mailSender = mailSender;
        _emails = emails;
        _logger = logger;
    }

    public async Task SendConfirmationLinkAsync(FinanceTrackerUser user, string email, string confirmationLink)
    {
        if (CanSend(user))
        {
            await _mailSender.SendAsync(await _emails.ConfirmEmailAsync(email, new Uri(confirmationLink)),
                CancellationToken.None);
        }
    }

    public async Task SendPasswordResetLinkAsync(FinanceTrackerUser user, string email, string resetLink)
    {
        if (CanSend(user))
        {
            await _mailSender.SendAsync(await _emails.ResetPasswordAsync(email, new Uri(resetLink)),
                CancellationToken.None);
        }
    }

    /// <summary>
    /// Only for Identity's own API, which FinTrack doesn't map; its pages send links.
    /// </summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public Task SendPasswordResetCodeAsync(FinanceTrackerUser user, string email, string resetCode) =>
        throw new NotSupportedException("FinTrack sends password reset links, not codes.");

    private bool CanSend(FinanceTrackerUser user)
    {
        if (_mailSender.IsAvailable)
        {
            return true;
        }

        _logger.LogAccountEmailSkipped(user.Id);
        return false;
    }
}
