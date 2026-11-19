using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Infrastructure.Budgets;

/// <summary>
/// The transactions a budget counts from, and which of their accounts stand in for others.
/// </summary>
/// <param name="Transactions">The transactions, those it covers among them.</param>
/// <param name="ShownCopies">The hidden copies of merged joint accounts whose transactions fill gaps in the shown
/// copy, by hidden copy, each with its shown copy. Empty for the user's own transactions.</param>
internal sealed record BudgetSpending(List<BankTransaction> Transactions, IReadOnlyDictionary<Guid, Guid> ShownCopies);
