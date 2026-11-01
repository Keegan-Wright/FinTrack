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
