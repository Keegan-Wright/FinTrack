using System.ComponentModel;

namespace FinanceTracker.Models.Response.Calendar;

public class CalendarTransactionItemResponse
{
    [Description("Amount of the scheduled transaction")]
    public required decimal Amount { get; init; }

    [Description("Description of the scheduled transaction")]
    public required string Description { get; init; }

    [Description("Type of the scheduled transaction")]
    public required string TransactionType { get; init; }

    [Description("Scheduled date and time of the transaction")]
    public required DateTime TransactionTime { get; init; }
}
