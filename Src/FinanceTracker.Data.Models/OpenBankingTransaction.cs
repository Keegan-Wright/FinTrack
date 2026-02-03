namespace FinanceTracker.Data.Models;

public class OpenBankingTransaction : BaseEntity
{
    public required string Description { get; set; }
    public required string TransactionType { get; set; }
    public required string TransactionCategory { get; set; }
    public required decimal Amount { get; set; }
    public required string Currency { get; set; }
    public required string TransactionId { get; set; }
    public required DateTime TransactionTime { get; set; }
    public required bool Pending { get; set; }

    public Guid ProviderId { get; set; }
    public OpenBankingProvider Provider { get; set; }

    public Guid AccountId { get; set; }
    public OpenBankingAccount Account { get; set; }

    public ICollection<OpenBankingTransactionClassifications> Classifications { get; set; }
}