namespace FinanceTracker.Domain.OpenBanking.Accounts;

/// <summary>
/// Point in time balance reported by the provider. For a card <see cref="Current"/> is what's owed, as a negative
/// amount, and <see cref="Available"/> the credit left to spend.
/// </summary>
/// <param name="CreditLimit">A card's credit limit, null for an account.</param>
public sealed record AccountBalance(
    decimal Available,
    decimal Current,
    DateTime AsOf,
    decimal? CreditLimit = null);
