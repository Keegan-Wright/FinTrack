using System.ComponentModel;
using FinanceTracker.Contracts.Bills;

namespace FinanceTracker.Contracts.Calendar;

/// <summary>
/// A bill due on a calendar day and how it stands.
/// </summary>
public class CalendarBillItem
{
    /// <summary>
    /// The bill's id.
    /// </summary>
    [Description("The bill's id")]
    public Guid BillId { get; init; }

    /// <summary>
    /// The bill's name.
    /// </summary>
    [Description("Name of the bill")]
    public required string Name { get; init; }

    /// <summary>
    /// What was paid once it's been paid, otherwise what's expected.
    /// </summary>
    [Description("What was paid once paid, otherwise what's expected")]
    public decimal Amount { get; init; }

    /// <summary>
    /// Whether it's been paid, is late or still to come.
    /// </summary>
    [Description("Whether the bill has been paid, is late or is still to come")]
    public BillState State { get; init; }
}
