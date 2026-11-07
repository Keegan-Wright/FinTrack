namespace FinanceTracker.Infrastructure.Notifications.Email;

/// <summary>
/// Sends email through the mail server in <see cref="MailSettings"/>.
/// </summary>
public interface IMailSender
{
    /// <summary>
    /// Whether email can be sent at all, a mail server is set up.
    /// </summary>
    bool IsAvailable { get; }

    /// <exception cref="InvalidOperationException">No mail server is set up, see <see cref="IsAvailable"/>.</exception>
    Task SendAsync(MailMessage message, CancellationToken cancellationToken);
}
