using System.ComponentModel;

namespace FinanceTracker.Contracts.Bills;

/// <summary>
/// One due date of a bill and how it stands.
/// </summary>
public class BillPeriod
{
    /// <summary>
    /// When the period is due.
    /// </summary>
    [Description("When the period is due")]
    public DateOnly Due { get; init; }

    /// <summary>
    /// What the period was expected to cost.
    /// </summary>
    [Description("What the period was expected to cost")]
    public decimal Expected { get; init; }

    /// <summary>
    /// How the period stands.
    /// </summary>
    [Description("How the period stands")]
    public BillState State { get; init; }

    /// <summary>
    /// Money out less refunds across its payments, 0 when there are none.
    /// </summary>
    [Description("What was paid for the period")]
    public decimal Paid { get; init; }

    /// <summary>
    /// The day of the period's latest payment, null when unpaid.
    /// </summary>
    [Description("The day of the period's latest payment, null when unpaid")]
    public DateOnly? PaidOn { get; init; }

    /// <summary>
    /// The transactions that paid the period.
    /// </summary>
    [Description("The transactions that paid the period")]
    public List<Guid> PaymentIds { get; init; } = [];
}
