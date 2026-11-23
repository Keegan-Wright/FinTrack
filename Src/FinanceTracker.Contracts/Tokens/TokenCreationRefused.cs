namespace FinanceTracker.Contracts.Tokens;

/// <summary>
/// The token wasn't made.
/// </summary>
/// <param name="Errors">Why, in words for the user.</param>
public sealed record TokenCreationRefused(IReadOnlyList<string> Errors) : TokenCreation;
