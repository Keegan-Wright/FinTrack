using FinanceTracker.Domain.Utility;

namespace FinanceTracker.Domain;

public class Debt : BaseEntity
{
    public Debt(string name, decimal monthlyPayment, DateTime? finalPaymentDate, DateTime? payOffGoal)
    {
        Name = name;
        MonthlyPayment = monthlyPayment;
        FinalPaymentDate = finalPaymentDate;
        PayOffGoal = payOffGoal;
    }

    [Encrypt] public string Name { get; private set; }

    [Encrypt] public decimal MonthlyPayment { get; private set; }

    [Encrypt] public DateTime? FinalPaymentDate { get; private set; }

    [Encrypt] public DateTime? PayOffGoal { get; private set; }
}
