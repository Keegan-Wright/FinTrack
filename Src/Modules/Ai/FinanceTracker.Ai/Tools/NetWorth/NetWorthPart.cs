using FinanceTracker.Contracts.NetWorth;

namespace FinanceTracker.Ai.Tools.NetWorth;

/// <summary>
/// One account, card or debt in today's net worth.
/// </summary>
/// <param name="Name">Its name.</param>
/// <param name="Kind">Account, Card or Debt.</param>
/// <param name="Amount">What it adds, negative for what's owed.</param>
public sealed record NetWorthPart(string Name, NetWorthItemKind Kind, decimal Amount);
