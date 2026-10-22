using System.ComponentModel;

namespace FinanceTracker.Contracts.Budget;

/// <summary>
/// A new budget category for the user.
/// </summary>
public class AddBudgetCategory
{
    /// <summary>
    /// The category's name.
    /// </summary>
    [Description("Name of the budget category")]
    public required string Name { get; init; }

    /// <summary>
    /// Money currently in the category.
    /// </summary>
    [Description("Current available funds in the budget category")]
    public required decimal AvailableFunds { get; init; }

    /// <summary>
    /// Money the category starts each month with.
    /// </summary>
    [Description("Starting amount for the monthly budget")]
    public required decimal MonthlyStart { get; init; }

    /// <summary>
    /// The amount the user is saving towards, 0 for no goal.
    /// </summary>
    [Description("Target savings goal for the budget category")]
    public required decimal SavingsGoal { get; init; }

    /// <summary>
    /// When the user wants to reach <see cref="SavingsGoal"/> by.
    /// </summary>
    [Description("Target date for achieving the savings goal")]
    public required DateTime? GoalCompletionDate { get; init; }
}
