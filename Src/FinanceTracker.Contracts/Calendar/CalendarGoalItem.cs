using System.ComponentModel;

namespace FinanceTracker.Contracts.Calendar;

/// <summary>
/// A budget category's savings goal falling due on a calendar day.
/// </summary>
public class CalendarGoalItem
{
    /// <summary>
    /// The budget category's name.
    /// </summary>
    [Description("Name of the financial goal")]
    public required string Name { get; init; }

    /// <summary>
    /// When the goal is due.
    /// </summary>
    [Description("Target date for completing the financial goal")]
    public required DateTime? GoalCompletionDate { get; init; }
}
