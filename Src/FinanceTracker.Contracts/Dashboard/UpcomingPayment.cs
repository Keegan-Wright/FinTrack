using System.ComponentModel;
using FinanceTracker.Contracts.Bills;

namespace FinanceTracker.Contracts.Dashboard;

/// <summary>
/// One of a bill's payments due soon or just gone, and whether it's been paid.
/// </summary>
public class UpcomingPayment
{
    /// <summary>
    /// The bill it's for.
    /// </summary>
    [Description("The bill the payment is for")]
    public Guid BillId { get; init; }

    /// <summary>
    /// The bill's name.
    /// </summary>
    [Description("Name of the bill the payment is for")]
    public required string PaymentName { get; init; }

    /// <summary>
    /// What was paid once it's been paid, otherwise what's expected.
    /// </summary>
    [Description("Amount of the payment, what was paid once paid")]
    public decimal Amount { get; init; }

    /// <summary>
    /// When it's due.
    /// </summary>
    [Description("Date when the payment is due")]
    public DateOnly Due { get; init; }

    /// <summary>
    /// Whether it's been paid, is late or still to come.
    /// </summary>
    [Description("Whether the payment has been paid, is late or is still to come")]
    public BillState State { get; init; }

    /// <summary>
    /// Where the bill came from, e.g. <c>Standing order</c> or <c>Subscription</c>.
    /// </summary>
    [Description("Where the bill came from, e.g. standing order or subscription")]
    public required string PaymentType { get; init; }
}
