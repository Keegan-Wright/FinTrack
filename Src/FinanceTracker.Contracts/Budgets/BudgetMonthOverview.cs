using System.ComponentModel;

namespace FinanceTracker.Contracts.Budgets;

/// <summary>
/// How all of the user's budgets are doing in one month, and what no budget covers.
/// </summary>
public class BudgetMonthOverview
{
    /// <summary>
    /// The month's first day.
    /// </summary>
    [Description("The month's first day")]
    public DateOnly Month { get; init; }

    /// <summary>
    /// Whether it's the current month. Earlier ones are finished, later ones haven't started.
    /// </summary>
    [Description("Whether it's the current month")]
    public bool IsCurrent { get; init; }

    /// <summary>
    /// The budgets running that month, by name.
    /// </summary>
    [Description("The budgets running that month")]
    public List<BudgetStatus> Budgets { get; init; } = [];

    /// <summary>
    /// Money out that month that no budget covers, so nothing goes unseen.
    /// </summary>
    [Description("Money out that month that no budget covers")]
    public decimal Unbudgeted { get; init; }
}
