using FinanceTracker.Domain.Utility;

namespace FinanceTracker.Domain;

public class OpenBankingTransaction : BaseEntity
{
    public OpenBankingTransaction(string description, string transactionType, string transactionCategory,
        decimal amount, string currency, string transactionId, DateTime transactionTime, bool pending, Guid providerId,
        Guid accountId)
    {
        Description = description;
        TransactionType = transactionType;
        TransactionCategory = transactionCategory;
        Amount = amount;
        Currency = currency;
        TransactionId = transactionId;
        TransactionTime = transactionTime;
        Pending = pending;
        ProviderId = providerId;
        AccountId = accountId;
    }

    [Encrypt] public string Description { get; private set; }

    [Encrypt] public string TransactionType { get; private set; }

    [Encrypt] public string TransactionCategory { get; private set; }

    [Encrypt] public decimal Amount { get; private set; }

    [Encrypt] public string Currency { get; private set; }

    [Encrypt] public string TransactionId { get; private set; }

    [Encrypt] public DateTime TransactionTime { get; private set; }

    [Encrypt] public bool Pending { get; private set; }

    public Guid ProviderId { get; private set; }
    public OpenBankingProvider? Provider { get; private set; }

    public Guid AccountId { get; private set; }
    public OpenBankingAccount? Account { get; private set; }

    public ICollection<OpenBankingTransactionClassifications>? Classifications { get; private set; }
}
