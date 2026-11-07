namespace FinanceTracker.Infrastructure.Notifications.Email;

/// <summary>
/// An email to one person, plain text with an HTML version.
/// </summary>
public sealed record MailMessage(string To, string Subject, string Text, string Html);
