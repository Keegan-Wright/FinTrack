using System.ComponentModel;

namespace FinanceTracker.Contracts.Calendar;

/// <summary>
/// A calendar event to add or the changes to one.
/// </summary>
public class SaveCalendarEvent
{
    /// <summary>
    /// What it is, e.g. a birthday.
    /// </summary>
    [Description("What the event is")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The day it's on, or first on for a yearly one.
    /// </summary>
    [Description("The day the event is on, or first on when it's yearly")]
    public DateOnly Date { get; set; }

    /// <summary>
    /// How often it comes round.
    /// </summary>
    [Description("How often the event comes round")]
    public EventRecurrence Recurrence { get; set; }

    /// <summary>
    /// Anything the user wants to remember, blank for none.
    /// </summary>
    [Description("Notes about the event")]
    public string? Notes { get; set; }

    /// <summary>
    /// What the user wants saved for it each time, null for nothing.
    /// </summary>
    [Description("What the user wants saved for the event each time")]
    public decimal? SavingsTarget { get; set; }

    /// <summary>
    /// One of the user's goals to save for it in, null for none or to create one with
    /// <see cref="CreateSavingsGoal"/>.
    /// </summary>
    [Description("An existing savings goal to save for the event in")]
    public Guid? SavingsGoalId { get; set; }

    /// <summary>
    /// Whether to create a goal for it, named after it and aimed at <see cref="SavingsTarget"/> by its next date.
    /// </summary>
    [Description("Whether to create a savings goal for the event")]
    public bool CreateSavingsGoal { get; set; }

    /// <summary>
    /// Whether its goal moves on to its next date once each passes, for a yearly event with a goal.
    /// </summary>
    [Description("Whether the savings goal moves on to the next date once each passes")]
    public bool RollsGoalOn { get; set; }
}
