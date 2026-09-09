using System.ComponentModel;

namespace FinanceTracker.Contracts.Calendar;

public class CalendarItems
{
    [Description("Date for which the calendar items are being returned")]
    public DateTime Date { get; init; }

    [Description("List of transactions scheduled for this date")]
    public IEnumerable<CalendarTransactionItem> Transactions { get; init; } = [];

    [Description("List of financial goals relevant to this date")]
    public IEnumerable<CalendarGoalItem> Goals { get; init; } = [];

    [Description("List of calendar events for this date")]
    public IEnumerable<CalendarEventItem> Events { get; init; } = [];
}
