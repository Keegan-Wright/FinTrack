using System.ComponentModel;

namespace FinanceTracker.Models.Response.Calendar;

public class CalendarItemsResponse
{
    [Description("Date for which the calendar items are being returned")]
    public DateTime Date { get; init; }

    [Description("List of transactions scheduled for this date")]
    public IEnumerable<CalendarTransactionItemResponse> Transactions { get; init; } = [];

    [Description("List of financial goals relevant to this date")]
    public IEnumerable<CalendarGoalItemResponse> Goals { get; init; } = [];

    [Description("List of calendar events for this date")]
    public IEnumerable<CalendarEventItemResponse> Events { get; init; } = [];
}
