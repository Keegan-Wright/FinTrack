using FinanceTracker.Domain.Utility;

namespace FinanceTracker.Domain.OpenBanking.Accounts;

/// <summary>
/// Point in time balance reported by the provider.
/// </summary>
public sealed record AccountBalance(
    [property: Encrypt] decimal Available,
    [property: Encrypt] decimal Current,
    DateTime AsOf);
