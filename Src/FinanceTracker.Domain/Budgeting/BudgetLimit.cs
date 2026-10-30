namespace FinanceTracker.Domain.Budgeting;

/// <summary>
/// What a <see cref="Budget"/> allows each month from <see cref="From"/> until a later limit takes over, so changing
/// it leaves the months before as they were.
/// </summary>
/// <param name="From">The first day of the first month it applies to.</param>
/// <param name="Amount">How much can be spent each month, 0 or more.</param>
public sealed record BudgetLimit(DateOnly From, decimal Amount)
{
    public BudgetMonth Month => BudgetMonth.Of(From);
}
