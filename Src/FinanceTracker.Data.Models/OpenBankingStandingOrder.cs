namespace FinanceTracker.Data.Models;

public class OpenBankingStandingOrder : BaseEntity
{
    public required string Frequency { get; set; }
    public required string Status { get; set; }
    public required string Currency { get; set; }
    public required DateTime NextPaymentDate { get; set; }
    public required decimal NextPaymentAmount { get; set; }
    public required DateTime FirstPaymentDate { get; set; }
    public required decimal FirstPaymentAmount { get; set; }
    public required DateTime FinalPaymentDate { get; set; }
    public required decimal FinalPaymentAmount { get; set; }
    public required string Reference { get; set; }
    public required string Payee { get; set; }
    public required DateTime Timestamp { get; set; }

    public Guid AccountId { get; set; }
    public OpenBankingAccount Account { get; set; }
}