namespace FinanceTracker.Data.Models;

public class OpenBankingTransactionClassifications : BaseEntity
{
    public required string Classification { get; set; }

    public Guid TransactionId { get; set; }
    public OpenBankingTransaction Transaction { get; set; }

    public bool IsCustomClassification { get; set; }

}