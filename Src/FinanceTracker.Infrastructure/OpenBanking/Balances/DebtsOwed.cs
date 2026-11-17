using FinanceTracker.Domain.Debts;
using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Infrastructure.OpenBanking.Balances;

/// <summary>
/// What a user's debts not linked to an account owed on any day, loaded once for totalling many days' net worth.
/// </summary>
/// <param name="Debts">The debts not linked to an account, a linked one being in its account's balance already.</param>
/// <param name="Payments">The user's transactions that pay off a debt.</param>
/// <param name="TimeZone">Where the user is, so a payment falls on the day it was where they are.</param>
internal sealed record DebtsOwed(
    IReadOnlyList<Debt> Debts,
    IReadOnlyList<BankTransaction> Payments,
    TimeZoneInfo TimeZone)
{
    /// <summary>
    /// What the debts owed on <paramref name="day"/>.
    /// </summary>
    /// <param name="day">The day.</param>
    public decimal On(DateOnly day) => Debts.Sum(x => DebtCalculator.OwedOn(x, Payments, TimeZone, day));
}
