using System.ComponentModel;

namespace FinanceTracker.Models.Request.Budget;

public class AddBudgetCategoryRequest
{
    [Description("Name of the budget category")]
    public required string Name { get; init; }

    [Description("Current available funds in the budget category")]
    public required decimal AvailableFunds { get; init; }

    [Description("Starting amount for the monthly budget")]
    public required decimal MonthlyStart { get; init; }

    [Description("Target savings goal for the budget category")]
    public required decimal SavingsGoal { get; init; }

    [Description("Target date for achieving the savings goal")]
    public required DateTime? GoalCompletionDate { get; init; }
}
