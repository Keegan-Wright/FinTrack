using FinanceTracker.Domain.Utility;

namespace FinanceTracker.Domain;

public class Debt : BaseEntity
{
    [Encrypt]
    public required string Name { get; init; }

    [Encrypt]
    public decimal MonthlyPayment { get; init; }

    [Encrypt]
    public DateTime? FinalPaymentDate { get; init; }

    [Encrypt]
    public DateTime? PayOffGoal { get; init; }
}
