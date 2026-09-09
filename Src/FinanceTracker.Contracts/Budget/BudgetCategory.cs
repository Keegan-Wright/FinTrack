namespace FinanceTracker.Contracts.Budget;

public class BudgetCategory
{
    public required string Name { get; init; }
    public decimal AvailableFunds { get; init; }
    public decimal MonthlyStart { get; init; }
    public decimal SavingsGoal { get; init; }
    public DateTime? GoalCompletionDate { get; init; }
}
