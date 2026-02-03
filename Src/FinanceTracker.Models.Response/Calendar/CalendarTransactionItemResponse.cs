using System.ComponentModel;

namespace FinanceTracker.Models.Response.Calendar;

public class CalendarTransactionItemResponse
{
    [Description("Amount of the scheduled transaction")]
    public decimal Amount { get; set; }

    [Description("Description of the scheduled transaction")]
    public string Description { get; set; }

    [Description("Type of the scheduled transaction")]
    public string TransactionType { get; set; }

    [Description("Scheduled date and time of the transaction")]
    public DateTime TransactionTime { get; set; }
}