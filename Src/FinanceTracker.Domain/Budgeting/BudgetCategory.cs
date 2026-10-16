using System.Diagnostics.CodeAnalysis;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Utility;

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
        AvailableFunds = availableFunds;
        ChangeMonthlyStart(monthlyStart);
        SetSavingsGoal(savingsGoal, goalCompletionDate);
    }

    [Encrypt] public string Name { get; private set; }

    /// <summary>
    /// Can go negative when the category is overspent.
    /// </summary>
    [Encrypt] public decimal AvailableFunds { get; private set; }

    [Encrypt] public decimal MonthlyStart { get; private set; }

    [Encrypt] public decimal SavingsGoal { get; private set; }

    [Encrypt] public DateTime? GoalCompletionDate { get; private set; }

    [MemberNotNull(nameof(Name))]
    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    public void AdjustAvailableFunds(decimal amount) => AvailableFunds += amount;

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
