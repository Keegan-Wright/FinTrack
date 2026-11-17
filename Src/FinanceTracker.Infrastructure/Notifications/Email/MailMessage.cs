using FinanceTracker.Emails;

namespace FinanceTracker.Infrastructure.Notifications.Email;

/// <summary>
/// An email to one person, plain text with an HTML version.
/// </summary>
/// <param name="To">Who it's sent to.</param>
/// <param name="Subject">Its subject.</param>
/// <param name="Text">The plain text.</param>
/// <param name="Html">The HTML version.</param>
public sealed record MailMessage(string To, string Subject, string Text, string Html)
{
    /// <summary>
    /// Images the HTML shows from inside the email, see <see cref="MailImage"/>.
    /// </summary>
    public IReadOnlyList<MailImage> Images { get; init; } = [];
}
