namespace FinanceTracker.Ai.Tools.Spending;

/// <summary>
/// One tag's or account's share of a period.
/// </summary>
/// <param name="Name">The tag or account.</param>
/// <param name="MoneyIn">Money received, positive.</param>
/// <param name="MoneyOut">Money spent, positive.</param>
/// <param name="Transactions">How many transactions were counted.</param>
public sealed record SpendingGroup(string Name, decimal MoneyIn, decimal MoneyOut, int Transactions);
