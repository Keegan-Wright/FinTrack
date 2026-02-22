using FinanceTracker.Data.Models.Utility;

namespace FinanceTracker.Data.Models;

public class Debt : BaseEntity
{
    [Encrypt]
    public required string Name { get; set; }

    [Encrypt]
    public decimal MonthlyPayment { get; set; }

    [Encrypt]
    public DateTime? FinalPaymentDate { get; set; }

    [Encrypt]
    public DateTime? PayOffGoal { get; set; }
}
