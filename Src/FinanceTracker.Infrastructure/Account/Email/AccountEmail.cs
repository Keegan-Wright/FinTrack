namespace FinanceTracker.Infrastructure.Account.Email;

/// <summary>
/// The words of one of <see cref="AccountEmails"/>, see <see cref="FinanceTracker.Emails.Templates.AccountEmailBody"/>.
/// </summary>
internal sealed record AccountEmail(string Subject, string Title, string Message, string ButtonText, string Reason);
