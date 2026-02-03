namespace FinanceTracker.Data.Models;

public class Debt : BaseEntity
{
    public required string Name { get; set; }
    public decimal MonthlyPayment { get; set; }
    public DateTime? FinalPaymentDate { get; set; }
    public DateTime? PayOffGoal { get; set; }
}