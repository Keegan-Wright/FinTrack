namespace FinanceTracker.Domain.OpenBanking.Accounts;

/// <summary>
/// Point in time balance reported by the provider. For a card <see cref="Current"/> is what's owed, as a negative
/// amount, and <see cref="Available"/> the credit left to spend.
/// </summary>
/// <param name="Available">What can be spent now, or for a card the credit left.</param>
/// <param name="Current">The balance, or for a card what's owed as a negative amount.</param>
/// <param name="AsOf">When the provider worked it out.</param>
/// <param name="CreditLimit">A card's credit limit, null for an account.</param>
public sealed record AccountBalance(
    decimal Available,
    decimal Current,
    DateTime AsOf,
    decimal? CreditLimit = null);
