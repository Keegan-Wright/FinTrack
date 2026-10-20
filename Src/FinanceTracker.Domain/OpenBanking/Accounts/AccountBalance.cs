namespace FinanceTracker.Domain.OpenBanking.Accounts;

/// <summary>
/// Point in time balance reported by the provider.
/// </summary>
public sealed record AccountBalance(
    decimal Available,
    decimal Current,
    DateTime AsOf);
