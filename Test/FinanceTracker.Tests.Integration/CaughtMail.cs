namespace FinanceTracker.Tests.Integration;

/// <summary>
/// An email the app sent, as the mail catcher caught it.
/// </summary>
/// <param name="InlineContentIds">The images carried inside it for its HTML to show, by content ID.</param>
public sealed record CaughtMail(string Subject, string Text, string Html, IReadOnlyList<string> InlineContentIds);
