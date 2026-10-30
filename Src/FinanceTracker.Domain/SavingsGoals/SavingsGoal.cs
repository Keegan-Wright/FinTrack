using System.Diagnostics.CodeAnalysis;
using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.SavingsGoals;

/// <summary>
/// Something the user is saving up for, e.g. £10,000 for a car by next summer.
/// </summary>
public sealed class SavingsGoal : AggregateRoot
{
    [Obsolete("EF Use Only")]
#pragma warning disable CS8618 // EF Core sets the properties when it materialises the entity
    private SavingsGoal()
    {
    }
#pragma warning restore CS8618

    public SavingsGoal(Guid userId, string name, decimal saved, decimal monthlyContribution, decimal target,
        DateTime? targetDate) : base(userId)
    {
        Rename(name);
        AddSavings(saved);
        ChangeMonthlyContribution(monthlyContribution);
        SetTarget(target, targetDate);
    }

    public string Name { get; private set; }

    /// <summary>
    /// How much is saved so far.
    /// </summary>
    public decimal Saved { get; private set; }

    /// <summary>
    /// How much the user puts towards it each month.
    /// </summary>
    public decimal MonthlyContribution { get; private set; }

    /// <summary>
    /// How much the user is saving up to, 0 for no target.
    /// </summary>
    public decimal Target { get; private set; }

    /// <summary>
    /// When the user wants to reach <see cref="Target"/> by.
    /// </summary>
    public DateTime? TargetDate { get; private set; }

    [MemberNotNull(nameof(Name))]
    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    public void AddSavings(decimal amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        Saved += amount;
    }

    public void ChangeMonthlyContribution(decimal monthlyContribution)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(monthlyContribution);
        MonthlyContribution = monthlyContribution;
    }

    public void SetTarget(decimal target, DateTime? targetDate)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(target);
        Target = target;
        TargetDate = targetDate;
    }
}
