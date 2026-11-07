namespace FinanceTracker.Tests.Integration;

/// <summary>
/// An email the app sent, as the mail catcher caught it.
/// </summary>
public sealed record CaughtMail(string Subject, string Text, string Html);
