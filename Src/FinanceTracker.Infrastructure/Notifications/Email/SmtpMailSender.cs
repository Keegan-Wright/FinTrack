using FinanceTracker.Emails;
using FinanceTracker.Telemetry;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace FinanceTracker.Infrastructure.Notifications.Email;

/// <summary>
/// <see cref="IMailSender"/> over SMTP with MailKit.
/// </summary>
public sealed class SmtpMailSender : IMailSender
{
    /// <summary>
    /// How long to wait on the mail server, short enough that a test email from the Notifications page doesn't hang.
    /// </summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly MailSettings _settings;
    private readonly FinTrackMetrics _metrics;

    /// <param name="settings">The mail server's settings.</param>
    /// <param name="metrics">Records its metrics.</param>
    public SmtpMailSender(MailSettings settings, FinTrackMetrics metrics)
    {
        _settings = settings;
        _metrics = metrics;
    }

    /// <inheritdoc/>
    public bool IsAvailable => _settings.IsConfigured;

    /// <inheritdoc/>
    public async Task SendAsync(MailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (_settings.Host is not { Length: > 0 } host)
        {
            throw new InvalidOperationException("No mail server is set up, set SMTP_HOST to send email.");
        }

        try
        {
            await SendThroughAsync(host, message, cancellationToken);
            _metrics.EmailSent(succeeded: true);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            _metrics.EmailSent(succeeded: false);
            throw;
        }
    }

    /// <summary>
    /// Connects to the mail server, signs in when set to, and sends.
    /// </summary>
    /// <param name="host">The mail server.</param>
    /// <param name="message">The email.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task SendThroughAsync(string host, MailMessage message, CancellationToken cancellationToken)
    {
        using MimeMessage mime = ToMime(message);
        using SmtpClient client = new() { Timeout = (int)Timeout.TotalMilliseconds };

        await client.ConnectAsync(host, _settings.Port, SocketOptions(_settings.Security), cancellationToken);
        if (!string.IsNullOrEmpty(_settings.Username))
        {
            await client.AuthenticateAsync(_settings.Username, _settings.Password ?? "", cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    /// <summary>
    /// The email as MailKit sends it, with the logo carried inside.
    /// </summary>
    /// <param name="message">The email.</param>
    private MimeMessage ToMime(MailMessage message)
    {
        MimeMessage mime = new();

        mime.From.Add(MailboxAddress.Parse(_settings.From));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;

        BodyBuilder body = new() { TextBody = message.Text, HtmlBody = message.Html };

        foreach (MailImage image in message.Images)
        {
            MimeEntity resource = body.LinkedResources.Add(image.FileName, image.Content.ToArray(),
                ContentType.Parse(image.ContentType));

            resource.ContentId = image.ContentId;
        }

        mime.Body = body.ToMessageBody();
        return mime;
    }

    /// <summary>
    /// MailKit's option for how the connection is secured.
    /// </summary>
    /// <param name="security">How it's secured.</param>
    private static SecureSocketOptions SocketOptions(MailSecurity security) => security switch
    {
        MailSecurity.StartTls => SecureSocketOptions.StartTls,
        MailSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
        MailSecurity.None => SecureSocketOptions.None,
        _ => SecureSocketOptions.Auto
    };
}
