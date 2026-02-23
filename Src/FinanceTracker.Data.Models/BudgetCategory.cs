using FinanceTracker.Data.Models.Utility;

namespace FinanceTracker.Data.Models;

public class BudgetCategory : BaseEntity
{
    [Encrypt]
    public required string Name { get; init; }

    [Encrypt]
    public decimal AvailableFunds { get; init; }

    [Encrypt]
    public decimal MonthlyStart { get; init; }

    [Encrypt]
    public decimal SavingsGoal { get; init; }

    [Encrypt]
    public DateTime? GoalCompletionDate { get; init; }
}
