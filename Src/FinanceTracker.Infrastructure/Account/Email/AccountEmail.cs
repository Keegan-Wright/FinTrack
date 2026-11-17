namespace FinanceTracker.Infrastructure.Account.Email;

/// <summary>
/// The words of one of <see cref="AccountEmails"/>, see <see cref="FinanceTracker.Emails.Templates.AccountEmailBody"/>.
/// </summary>
/// <param name="Subject">The email's subject.</param>
/// <param name="Title">The heading.</param>
/// <param name="Message">What it says.</param>
/// <param name="ButtonText">What the button says.</param>
/// <param name="Reason">Why the email came.</param>
internal sealed record AccountEmail(string Subject, string Title, string Message, string ButtonText, string Reason);
