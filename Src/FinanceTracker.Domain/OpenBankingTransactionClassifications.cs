using FinanceTracker.Domain.Utility;

namespace FinanceTracker.Domain;

public class OpenBankingTransactionClassifications : BaseEntity
{
    public OpenBankingTransactionClassifications(string classification, Guid transactionId, bool isCustomClassification)
    {
        Classification = classification;
        TransactionId = transactionId;
        IsCustomClassification = isCustomClassification;
    }

    [Encrypt] public string Classification { get; private set; }

    public Guid TransactionId { get; private set; }
    public OpenBankingTransaction? Transaction { get; private set; }

    public bool IsCustomClassification { get; private set; }
}
