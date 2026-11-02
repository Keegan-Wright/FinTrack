namespace FinanceTracker.Domain.Debts;

/// <summary>
/// What a debt owed on a day, a point in its history from <see cref="DebtCalculator.History"/>.
/// </summary>
public sealed record DebtBalance(DateOnly On, decimal Owed);
