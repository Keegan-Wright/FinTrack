using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure.Account.Email;
using FinanceTracker.Infrastructure.Notifications.Email;

namespace FinanceTracker.Components.Account;

/// <inheritdoc cref="ISecurityEmails"/>
public sealed class SecurityEmails : ISecurityEmails
{
    private readonly IMailSender _mailSender;
    private readonly IAccountEmails _accountEmails;
    private readonly ILogger<SecurityEmails> _logger;

    /// <param name="mailSender">Sends the emails.</param>
    /// <param name="accountEmails">Writes them.</param>
    /// <param name="logger">Its logger.</param>
    public SecurityEmails(IMailSender mailSender, IAccountEmails accountEmails, ILogger<SecurityEmails> logger)
    {
        _mailSender = mailSender;
        _accountEmails = accountEmails;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task TwoFactorOnAsync(FinanceTrackerUser user) =>
        SendAsync(user, _accountEmails.TwoFactorOnAsync);

    /// <inheritdoc/>
    public Task TwoFactorOffAsync(FinanceTrackerUser user, TwoFactorOffCause cause) =>
        SendAsync(user, to => _accountEmails.TwoFactorOffAsync(to, cause));

    /// <inheritdoc/>
    public Task RecoveryCodesMadeAsync(FinanceTrackerUser user) =>
        SendAsync(user, _accountEmails.RecoveryCodesMadeAsync);

    /// <inheritdoc/>
    public Task PasskeyAddedAsync(FinanceTrackerUser user, string name) =>
        SendAsync(user, to => _accountEmails.PasskeyAddedAsync(to, name));

    /// <inheritdoc/>
    public Task PasskeyRemovedAsync(FinanceTrackerUser user, string name) =>
        SendAsync(user, to => _accountEmails.PasskeyRemovedAsync(to, name));

    /// <inheritdoc/>
    public Task PasskeysRemovedByAdminAsync(FinanceTrackerUser user) =>
        SendAsync(user, _accountEmails.PasskeysRemovedByAdminAsync);

    /// <summary>
    /// Sends the email <paramref name="write"/> makes to <paramref name="user"/>'s confirmed email. Logs it instead
    /// when there's no mail server or it fails, the change it's about has already been made.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="write">Writes the email to the address given.</param>
    private async Task SendAsync(FinanceTrackerUser user, Func<string, Task<MailMessage>> write)
    {
        // An unconfirmed email might not be theirs, so it isn't told about their sign in
        if (!user.EmailConfirmed || string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }

        if (!_mailSender.IsAvailable)
        {
            _logger.LogAccountEmailSkipped(user.Id);
            return;
        }

        try
        {
            // Not the request's token. The change is saved, so the user is told even if they've gone
            await _mailSender.SendAsync(await write(user.Email), CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogSecurityEmailFailed(exception, user.Id);
        }
    }
}
