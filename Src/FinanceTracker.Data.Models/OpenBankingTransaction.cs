using FinanceTracker.Data.Models.Utility;

namespace FinanceTracker.Data.Models;

public class OpenBankingTransaction : BaseEntity
{
    
    [Encrypt]
    public required string Description { get; set; }
    
    [Encrypt]
    public required string TransactionType { get; set; }
    
    [Encrypt]
    public required string TransactionCategory { get; set; }
    
    [Encrypt]
    public required decimal Amount { get; set; }
    
    [Encrypt]
    public required string Currency { get; set; }
    
    [Encrypt]
    public required string TransactionId { get; set; }
    
    [Encrypt]
    public required DateTime TransactionTime { get; set; }
    
    [Encrypt]
    public required bool Pending { get; set; }

    public Guid ProviderId { get; set; }
    public OpenBankingProvider Provider { get; set; }

    public Guid AccountId { get; set; }
    public OpenBankingAccount Account { get; set; }

    public ICollection<OpenBankingTransactionClassifications> Classifications { get; set; }
}