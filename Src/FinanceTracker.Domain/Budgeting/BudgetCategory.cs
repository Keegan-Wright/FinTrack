using System.Diagnostics.CodeAnalysis;
using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Budgeting;

/// <summary>
/// User defined category for budgeting goals.
/// </summary>
public sealed class BudgetCategory : AggregateRoot
{
    [Obsolete("EF Use Only")]
    private BudgetCategory()
    {
    }

    public BudgetCategory(Guid userId, string name, decimal availableFunds, decimal monthlyStart,
        decimal savingsGoal, DateTime? goalCompletionDate) : base(userId)
    {
        Rename(name);
        AdjustAvailableFunds(availableFunds);
        ChangeMonthlyStart(monthlyStart);
        SetSavingsGoal(savingsGoal, goalCompletionDate);
    }

    public string Name { get; private set; }

    public decimal AvailableFunds { get; private set; }

    public decimal MonthlyStart { get; private set; }

    public decimal SavingsGoal { get; private set; }

    public DateTime? GoalCompletionDate { get; private set; }

    [MemberNotNull(nameof(Name))]
    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    public void AdjustAvailableFunds(decimal amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        AvailableFunds += amount;
    }

    public void ChangeMonthlyStart(decimal monthlyStart)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(monthlyStart);
        MonthlyStart = monthlyStart;
    }

    public void SetSavingsGoal(decimal savingsGoal, DateTime? goalCompletionDate)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(savingsGoal);
        SavingsGoal = savingsGoal;
        GoalCompletionDate = goalCompletionDate;
    }
}
