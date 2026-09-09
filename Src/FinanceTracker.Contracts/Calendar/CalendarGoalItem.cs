using System.ComponentModel;

namespace FinanceTracker.Contracts.Calendar;

public class CalendarGoalItem
{
    [Description("Name of the financial goal")]
    public required string Name { get; init; }

    [Description("Target date for completing the financial goal")]
    public required DateTime? GoalCompletionDate { get; init; }
}
