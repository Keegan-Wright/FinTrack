using System.ComponentModel;

namespace FinanceTracker.Models.Response.Dashboard;

public class UpcomingPaymentsResponse
{
    [Description("Name or description of the upcoming payment")]
    public string PaymentName { get; set; }
         
    [Description("Amount of the upcoming payment")]
    public decimal Amount { get; set; }
         
    [Description("Date when the payment is due")]
    public DateTime PaymentDate { get; set; }

    [Description("Type or category of the payment (e.g., Bill, Subscription, etc.)")]
    public string PaymentType { get; set; }

}