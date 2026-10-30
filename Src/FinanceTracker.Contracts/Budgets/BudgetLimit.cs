using System.ComponentModel;

namespace FinanceTracker.Contracts.Budgets;

/// <summary>
/// A budget's limit from one month until a later one takes over.
/// </summary>
public class BudgetLimit
{
    /// <summary>
    /// The first month it applies to, as its first day.
    /// </summary>
    [Description("The first month the limit applies to, as its first day")]
    public DateOnly From { get; init; }

    /// <summary>
    /// How much can be spent each month.
    /// </summary>
    [Description("How much can be spent each month")]
    public decimal Amount { get; init; }
}
