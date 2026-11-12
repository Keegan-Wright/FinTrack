using System.ComponentModel;

namespace FinanceTracker.Contracts.Calendar;

/// <summary>
/// Something the user put on their calendar to plan saving around.
/// </summary>
public class CalendarEvent
{
    /// <summary>
    /// Unique identifier for the event.
    /// </summary>
    [Description("Unique identifier for the event")]
    public Guid Id { get; init; }

    /// <summary>
    /// What it is, e.g. a birthday.
    /// </summary>
    [Description("What the event is")]
    public required string Title { get; init; }

    /// <summary>
    /// The day it's on, or first on for a yearly one.
    /// </summary>
    [Description("The day the event is on, or first on when it's yearly")]
    public DateOnly Date { get; init; }

    /// <summary>
    /// How often it comes round.
    /// </summary>
    [Description("How often the event comes round")]
    public EventRecurrence Recurrence { get; init; }

    /// <summary>
    /// Anything the user wants to remember, null for none.
    /// </summary>
    [Description("Notes about the event")]
    public string? Notes { get; init; }

    /// <summary>
    /// What the user wants saved for it each time, null for nothing.
    /// </summary>
    [Description("What the user wants saved for the event each time")]
    public decimal? SavingsTarget { get; init; }

    /// <summary>
    /// The goal saving for it, null for none.
    /// </summary>
    [Description("The savings goal saving for the event")]
    public Guid? SavingsGoalId { get; init; }

    /// <summary>
    /// The name of <see cref="SavingsGoalId"/>, null without a goal.
    /// </summary>
    [Description("The name of the savings goal")]
    public string? SavingsGoalName { get; init; }

    /// <summary>
    /// Whether its goal moves on to its next date, with <see cref="SavingsTarget"/> as its target, once each passes.
    /// </summary>
    [Description("Whether the savings goal moves on to the next date once each passes")]
    public bool RollsGoalOn { get; init; }

    /// <summary>
    /// The next day it's on from today, null for a one-off that's passed.
    /// </summary>
    [Description("The next day the event is on, null once a one-off has passed")]
    public DateOnly? NextOn { get; init; }

    /// <summary>
    /// The first and last name of whoever shares it with the user, null when it's their own.
    /// </summary>
    [Description("Who shares the event with the user, empty when it's their own")]
    public string? SharedBy { get; init; }

    /// <summary>
    /// Whether the user can change its title, date, repeat and notes: their own, or shared with them with changes
    /// allowed. Only its owner changes what it saves towards or deletes it.
    /// </summary>
    [Description("Whether the user can edit the event")]
    public bool CanChange { get; init; } = true;
}
