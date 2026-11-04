using System.ComponentModel;

namespace FinanceTracker.Contracts.Bills;

/// <summary>
/// The user's bills in one month.
/// </summary>
public class BillMonthOverview
{
    /// <summary>
    /// The first day of the month.
    /// </summary>
    [Description("The first day of the month")]
    public DateOnly Month { get; init; }

    /// <summary>
    /// The period's first day the pay day when the user's months run from pay to pay, otherwise the month's first
    /// day. Bills due from here to <see cref="End"/> are the month's.
    /// </summary>
    [Description("The period's first day, the pay day or the month's first day")]
    public DateOnly Start { get; init; }

    /// <summary>
    /// The period's last day, inclusive the day before the next pay, otherwise the month's last day.
    /// </summary>
    [Description("The period's last day, inclusive")]
    public DateOnly End { get; init; }

    /// <summary>
    /// Whether this is the current month.
    /// </summary>
    [Description("Whether this is the current month")]
    public bool IsCurrent { get; init; }

    /// <summary>
    /// What the bills running in the month cost a month, each spread over a month whatever its frequency.
    /// </summary>
    [Description("What the bills running in the month cost a month")]
    public decimal Committed { get; init; }

    /// <summary>
    /// What the periods due in the month were expected to cost.
    /// </summary>
    [Description("What the periods due in the month were expected to cost")]
    public decimal Due { get; init; }

    /// <summary>
    /// What was paid for the periods due in the month.
    /// </summary>
    [Description("What was paid for the periods due in the month")]
    public decimal Paid { get; init; }

    /// <summary>
    /// The bills running at any point in the month, by name.
    /// </summary>
    [Description("The bills running in the month")]
    public List<BillStatus> Bills { get; init; } = [];
}
