using System.ComponentModel;

namespace FinanceTracker.Contracts.Dashboard;

/// <summary>
/// A standing order or direct debit payment due soon.
/// </summary>
public class UpcomingPayment
{
    /// <summary>
    /// The payee, or the direct debit's name.
    /// </summary>
    [Description("Name or description of the upcoming payment")]
    public required string PaymentName { get; init; }

    /// <summary>
    /// The payment's amount as the bank reports it, for a direct debit the previous payment's. The sign varies by bank,
    /// show it as <see cref="Math.Abs(decimal)"/>.
    /// </summary>
    [Description("Amount of the upcoming payment")]
    public decimal Amount { get; init; }

    /// <summary>
    /// When the payment is due. Estimated for direct debits, see <see
    /// cref="Services.IDashboardService.GetUpcomingPaymentsAsync"/>.
    /// </summary>
    [Description("Date when the payment is due")]
    public DateTime PaymentDate { get; init; }

    /// <summary>
    /// <c>Standing Order</c> or <c>Direct Debit</c>.
    /// </summary>
    [Description("Type or category of the payment (e.g., Bill, Subscription, etc.)")]
    public required string PaymentType { get; init; }
}
