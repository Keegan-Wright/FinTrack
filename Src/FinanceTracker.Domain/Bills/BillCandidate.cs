using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Domain.Bills;

/// <summary>
/// A transaction that could pay one period of a bill, weighed by <see cref="BillMatcher"/>.
/// </summary>
/// <param name="AmountGap">How far it is from the expected amount, as a fraction of it.</param>
/// <param name="DayGap">How many days it is from the due date.</param>
internal sealed record BillCandidate(BankTransaction Transaction, Bill Bill, DateOnly Due, decimal AmountGap,
    int DayGap);
