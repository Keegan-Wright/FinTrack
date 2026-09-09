using FinanceTracker.Domain.Utility;

namespace FinanceTracker.Domain;

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
