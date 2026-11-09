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

    public required string? Host { get; init; }

    public int Port { get; init; } = 587;

    public MailSecurity Security { get; init; } = MailSecurity.Auto;

    public string? Username { get; init; }

    public string? Password { get; init; }

    /// <summary>
    /// Who emails come from, e.g. <c>fintrack@example.com</c>.
    /// </summary>
    public required string From { get; init; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
}
