using System.ComponentModel;

namespace FinanceTracker.Contracts.Calendar;

/// <summary>
/// Everything on one calendar day.
/// </summary>
public class CalendarItems
{
    /// <summary>
    /// The day.
    /// </summary>
    [Description("Date for which the calendar items are being returned")]
    public DateTime Date { get; init; }

    /// <summary>
    /// Transactions that happened on the day.
    /// </summary>
    [Description("List of transactions scheduled for this date")]
    public IEnumerable<CalendarTransactionItem> Transactions { get; init; } = [];

    /// <summary>
    /// Savings goals due on the day.
    /// </summary>
    [Description("List of financial goals relevant to this date")]
    public IEnumerable<CalendarGoalItem> Goals { get; init; } = [];

    /// <summary>
    /// Bills due on the day, paid or not.
    /// </summary>
    [Description("List of bills due on this date and how each stands")]
    public IEnumerable<CalendarBillItem> Bills { get; init; } = [];

    /// <summary>
    /// User events on the day. Always empty for now, see <see cref="CalendarEventItem"/>.
    /// </summary>
    [Description("List of calendar events for this date")]
    public IEnumerable<CalendarEventItem> Events { get; init; } = [];
}
