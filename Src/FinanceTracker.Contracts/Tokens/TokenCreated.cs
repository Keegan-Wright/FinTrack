namespace FinanceTracker.Contracts.Tokens;

/// <summary>
/// The token was made.
/// </summary>
/// <param name="Token">The whole token, shown to the user this once.</param>
public sealed record TokenCreated(string Token) : TokenCreation;
