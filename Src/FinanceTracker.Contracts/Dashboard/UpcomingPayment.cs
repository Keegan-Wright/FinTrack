using System.ComponentModel;

namespace FinanceTracker.Contracts.Dashboard;

public class UpcomingPayment
{
    [Description("Name or description of the upcoming payment")]
    public required string PaymentName { get; init; }

    [Description("Amount of the upcoming payment")]
    public decimal Amount { get; init; }

    [Description("Date when the payment is due")]
    public DateTime PaymentDate { get; init; }

    [Description("Type or category of the payment (e.g., Bill, Subscription, etc.)")]
    public required string PaymentType { get; init; }
}
