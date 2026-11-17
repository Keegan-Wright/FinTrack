namespace FinanceTracker.Infrastructure.Notifications.Email;

/// <summary>
/// The mail server emails go through, from the <c>SMTP_*</c> settings. Without a host there's no email.
/// </summary>
public sealed class MailSettings
{
    /// <summary>
    /// No mail server, so no email.
    /// </summary>
    public static MailSettings None { get; } = new() { Host = null, From = "" };

    /// <summary>
    /// The mail server, null for no email.
    /// </summary>
    public required string? Host { get; init; }

    /// <summary>
    /// The mail server's port, 587 by default.
    /// </summary>
    public int Port { get; init; } = 587;

    /// <summary>
    /// How the connection is secured, chosen from the port by default.
    /// </summary>
    public MailSecurity Security { get; init; } = MailSecurity.Auto;

    /// <summary>
    /// Who to sign in as, null to not sign in.
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// The password for <see cref="Username"/>.
    /// </summary>
    public string? Password { get; init; }

    /// <summary>
    /// Who emails come from, e.g. <c>fintrack@example.com</c>.
    /// </summary>
    public required string From { get; init; }

    /// <summary>
    /// Whether there's a mail server to send through.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
}
