using FinanceTracker.Data.Models.Utility;

namespace FinanceTracker.Data.Models;

public class OpenBankingTransactionClassifications : BaseEntity
{
    [Encrypt]
    public required string Classification { get; set; }

    public Guid TransactionId { get; set; }
    public OpenBankingTransaction Transaction { get; set; }

    public bool IsCustomClassification { get; set; }
}
