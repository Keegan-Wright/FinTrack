namespace FinanceTracker.Contracts.Budget;

/// <summary>
/// One of the user's budget categories.
/// </summary>
public class BudgetCategory
{
    /// <summary>
    /// The category's name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Money currently in the category.
    /// </summary>
    public decimal AvailableFunds { get; init; }

    /// <summary>
    /// Money the category starts each month with.
    /// </summary>
    public decimal MonthlyStart { get; init; }

    /// <summary>
    /// The amount the user is saving towards, 0 for no goal.
    /// </summary>
    public decimal SavingsGoal { get; init; }

    /// <summary>
    /// When the user wants to reach <see cref="SavingsGoal"/> by.
    /// </summary>
    public DateTime? GoalCompletionDate { get; init; }
}
