using System.ComponentModel;

namespace FinanceTracker.Contracts.Calendar;

/// <summary>
/// A calendar event on a day, with how its savings stand.
/// </summary>
public class CalendarEventItem
{
    /// <summary>
    /// The event's id.
    /// </summary>
    [Description("The event's id")]
    public Guid EventId { get; init; }

    /// <summary>
    /// What it is.
    /// </summary>
    [Description("What the event is")]
    public required string Title { get; init; }

    /// <summary>
    /// Anything the user wants to remember, null for none.
    /// </summary>
    [Description("Notes about the event")]
    public string? Notes { get; init; }

    /// <summary>
    /// Whether it comes round every year.
    /// </summary>
    [Description("Whether the event comes round every year")]
    public bool IsYearly { get; init; }

    /// <summary>
    /// What the user wants saved for it, null for nothing.
    /// </summary>
    [Description("What the user wants saved for the event")]
    public decimal? SavingsTarget { get; init; }

    /// <summary>
    /// The goal saving for it, null for none.
    /// </summary>
    [Description("Name of the savings goal saving for the event")]
    public string? SavingsGoalName { get; init; }

    /// <summary>
    /// What's saved in that goal, null without one.
    /// </summary>
    [Description("What's saved in the savings goal")]
    public decimal? Saved { get; init; }
}
