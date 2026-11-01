using System.ComponentModel;

namespace FinanceTracker.Contracts.Bills;

/// <summary>
/// One bill and its periods in a month.
/// </summary>
public class BillStatus
{
    /// <summary>
    /// The bill.
    /// </summary>
    [Description("The bill")]
    public required Bill Bill { get; init; }

    /// <summary>
    /// Its periods due in the month, oldest first. Several for a weekly bill, none in most months for a quarterly
    /// one.
    /// </summary>
    [Description("The bill's periods due in the month")]
    public List<BillPeriod> Periods { get; init; } = [];
}
