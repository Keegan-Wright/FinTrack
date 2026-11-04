using System.ComponentModel;

namespace FinanceTracker.Contracts.Budgets;

/// <summary>
/// How all of the user's budgets are doing in one month, and what no budget covers. The month is the user's period
/// named after it, from pay to pay once their pay is picked up, see <see cref="Start"/>.
/// </summary>
public class BudgetMonthOverview
{
    /// <summary>
    /// The month's first day.
    /// </summary>
    [Description("The month's first day")]
    public DateOnly Month { get; init; }

    /// <summary>
    /// The period's first day the pay day when the user's periods run from pay to pay, otherwise the month's first
    /// day.
    /// </summary>
    [Description("The period's first day, the pay day or the month's first day")]
    public DateOnly Start { get; init; }

    /// <summary>
    /// The period's last day, the day before the next pay, otherwise the month's last day.
    /// </summary>
    [Description("The period's last day, inclusive")]
    public DateOnly End { get; init; }

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
    /// Money out that month that no budget covers, less card refunds (money in the bank calls a purchase), never
    /// below 0.
    /// </summary>
    [Description("Money out that month that no budget covers, less card refunds")]
    public decimal Unbudgeted { get; init; }
}
