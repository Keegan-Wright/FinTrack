namespace FinanceTracker.Data.Models;

public class OpenBankingDirectDebit : BaseEntity
{
    public required string OpenBankingDirectDebitId { get; set; }
    public required string Name { get; set; }
    public required string Status { get; set; }
    public required DateTime PreviousPaymentTimeStamp { get; set; }
    public required decimal PreviousPaymentAmount { get; set; }
    public required string Currency { get; set; }
    public required DateTime TimeStamp { get; set; }

    public Guid AccountId { get; set; }
    public OpenBankingAccount Account { get; set; }
}