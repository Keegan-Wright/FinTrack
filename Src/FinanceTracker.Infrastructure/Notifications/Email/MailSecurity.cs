namespace FinanceTracker.Infrastructure.Notifications.Email;

/// <summary>
/// How the connection to the mail server is secured, see <c>SMTP_SECURITY</c>.
/// </summary>
public enum MailSecurity
{
    /// <summary>
    /// Chosen from the port, SSL on connect on 465, STARTTLS when the server offers it otherwise.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Plain, then upgraded with STARTTLS, which has to succeed. Usually port 587.
    /// </summary>
    StartTls = 1,

    /// <summary>
    /// TLS from the start, usually port 465.
    /// </summary>
    SslOnConnect = 2,

    /// <summary>
    /// Unencrypted, only for a mail server on the same machine or network, e.g. a development mail catcher.
    /// </summary>
    None = 3
}
