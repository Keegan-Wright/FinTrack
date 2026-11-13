using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Infrastructure.Debts;

/// <summary>
/// What a debt's figures are worked out from, the same whoever sees it: its linked account, and its payments from
/// the accounts its owner sees.
/// </summary>
/// <param name="Account">The account it follows, null when it isn't linked or has gone.</param>
/// <param name="Payments">The transactions paying it off.</param>
internal sealed record DebtFigures(BankAccount? Account, List<BankTransaction> Payments);
