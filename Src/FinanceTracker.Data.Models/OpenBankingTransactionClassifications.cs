using FinanceTracker.Data.Models.Utility;

namespace FinanceTracker.Data.Models;

public class OpenBankingTransactionClassifications : BaseEntity
{
    [Encrypt]
    public required string Classification { get; init; }

    public Guid TransactionId { get; init; }
    public OpenBankingTransaction? Transaction { get; set; }

    public bool IsCustomClassification { get; init; }
}
