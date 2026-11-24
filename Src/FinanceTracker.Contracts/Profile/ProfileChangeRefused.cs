namespace FinanceTracker.Contracts.Profile;

/// <summary>
/// The change wasn't made.
/// </summary>
/// <param name="Errors">Why, in words for the user, e.g. the current password was wrong.</param>
public sealed record ProfileChangeRefused(IReadOnlyList<string> Errors) : ProfileChange;
