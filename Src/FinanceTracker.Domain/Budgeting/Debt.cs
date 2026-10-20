using System.Diagnostics.CodeAnalysis;
using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Budgeting;

/// <summary>
/// A user defined debt for tracking peyments and progress.
/// </summary>
public sealed class Debt : AggregateRoot
{
    [Obsolete("EF Use Only")]
    private Debt()
    {
    }

    public Debt(Guid userId, string name, decimal monthlyPayment, DateTime? finalPaymentDate, DateTime? payOffGoal)
        : base(userId)
    {
        Rename(name);
        ChangeMonthlyPayment(monthlyPayment);
        FinalPaymentDate = finalPaymentDate;
        PayOffGoal = payOffGoal;
    }

    public string Name { get; private set; }

    public decimal MonthlyPayment { get; private set; }

    public DateTime? FinalPaymentDate { get; private set; }

    public DateTime? PayOffGoal { get; private set; }

    [MemberNotNull(nameof(Name))]
    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    public void ChangeMonthlyPayment(decimal monthlyPayment)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(monthlyPayment);
        MonthlyPayment = monthlyPayment;
    }

    public void ChangeFinalPaymentDate(DateTime? finalPaymentDate) => FinalPaymentDate = finalPaymentDate;

    public void SetPayOffGoal(DateTime? payOffGoal) => PayOffGoal = payOffGoal;
}
