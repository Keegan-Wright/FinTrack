using FinanceTracker.Data.Models.Utility;

namespace FinanceTracker.Data.Models;

public class BudgetCategory : BaseEntity
{
    [Encrypt]
    public required string Name { get; set; }

    [Encrypt]
    public decimal AvailableFunds { get; set; }

    [Encrypt]
    public decimal MonthlyStart { get; set; }

    [Encrypt]
    public decimal SavingsGoal { get; set; }

    [Encrypt]
    public DateTime? GoalCompletionDate { get; set; }
}
