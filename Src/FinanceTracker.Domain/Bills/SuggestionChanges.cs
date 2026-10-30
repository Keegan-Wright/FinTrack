namespace FinanceTracker.Domain.Bills;

/// <summary>
/// What <see cref="SubscriptionDetector.Suggest"/> changed.
/// </summary>
/// <param name="Added">New suggestions, for the caller to save.</param>
/// <param name="Removed">Suggestions waiting on the user that no longer apply, for the caller to delete.</param>
public sealed record SuggestionChanges(IReadOnlyList<BillSuggestion> Added, IReadOnlyList<BillSuggestion> Removed);
