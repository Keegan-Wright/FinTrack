using System.ComponentModel;

namespace FinanceTracker.Models.Response.Calendar;

public class CalendarGoalItemResponse
{
    [Description("Name of the financial goal")]
    public string Name { get; set; }

    [Description("Target date for completing the financial goal")]
    public DateTime? GoalCompletionDate { get; set; }
}
