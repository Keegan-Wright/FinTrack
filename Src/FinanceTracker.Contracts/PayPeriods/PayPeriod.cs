using System.ComponentModel;

namespace FinanceTracker.Contracts.PayPeriods;

/// <summary>
/// The days from one pay to the day before the next, named after the month it mostly covers. A calendar month when
/// there's no pay to go by.
/// </summary>
public class PayPeriod
{
    /// <summary>
    /// The pay day, or the 1st for a calendar month.
    /// </summary>
    [Description("The first day")]
    public DateOnly Start { get; init; }

    /// <summary>
    /// The day before the next pay, inclusive.
    /// </summary>
    [Description("The last day, inclusive")]
    public DateOnly End { get; init; }

    /// <summary>
    /// The first of the month it's named after, e.g. 1 November for pay on 30 October.
    /// </summary>
    [Description("The first of the month it's named after")]
    public DateOnly Month { get; init; }
}
