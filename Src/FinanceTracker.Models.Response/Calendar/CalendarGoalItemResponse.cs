using System.ComponentModel;

namespace FinanceTracker.Models.Response.Calendar;

public class CalendarGoalItemResponse
{
    [Description("Name of the financial goal")]
    public required string Name { get; init; }

    [Description("Target date for completing the financial goal")]
    public required DateTime? GoalCompletionDate { get; init; }
}
