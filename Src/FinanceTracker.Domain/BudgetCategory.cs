using FinanceTracker.Domain.Utility;

namespace FinanceTracker.Domain;

public class BudgetCategory : BaseEntity
{
    public BudgetCategory(string name, decimal availableFunds, decimal monthlyStart, decimal savingsGoal,
        DateTime? goalCompletionDate)
    {
        Name = name;
        AvailableFunds = availableFunds;
        MonthlyStart = monthlyStart;
        SavingsGoal = savingsGoal;
        GoalCompletionDate = goalCompletionDate;
    }

    [Encrypt] public string Name { get; private set; }

    [Encrypt] public decimal AvailableFunds { get; private set; }

    [Encrypt] public decimal MonthlyStart { get; private set; }

    [Encrypt] public decimal SavingsGoal { get; private set; }

    [Encrypt] public DateTime? GoalCompletionDate { get; private set; }
}
