namespace FinanceTracker.Contracts.Profile;

/// <summary>
/// The change wasn't made.
/// </summary>
/// <param name="Errors">Why, in words for the user.</param>
public sealed record PasskeyChangeRefused(IReadOnlyList<string> Errors) : PasskeyChange;
