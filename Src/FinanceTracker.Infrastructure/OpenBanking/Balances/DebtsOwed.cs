using FinanceTracker.Domain.Debts;
using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Infrastructure.OpenBanking.Balances;

/// <summary>
/// What a user's debts not linked to an account owed on any day, loaded once for totalling many days' net worth.
/// </summary>
/// <param name="Debts">The debts not linked to an account, a linked one being in its account's balance already.</param>
/// <param name="Payments">The user's transactions that pay off a debt.</param>
internal sealed record DebtsOwed(
    IReadOnlyList<Debt> Debts,
    IReadOnlyList<BankTransaction> Payments,
    TimeZoneInfo TimeZone)
{
    public decimal On(DateOnly day) => Debts.Sum(x => DebtCalculator.OwedOn(x, Payments, TimeZone, day));
}
