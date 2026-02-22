namespace FinanceTracker.Models.Response.Budget;

public class BudgetCategoryResponse
{
    public required string Name { get; set; }
    public decimal AvailableFunds { get; set; }
    public decimal MonthlyStart { get; set; }
    public decimal SavingsGoal { get; set; }
    public DateTime? GoalCompletionDate { get; set; }
}
