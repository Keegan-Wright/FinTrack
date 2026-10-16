using FinanceTracker.Domain.Utility;

namespace FinanceTracker.Domain;

public class OpenBankingDirectDebit : BaseEntity
{
    public OpenBankingDirectDebit(string openBankingDirectDebitId, string name, string status,
        DateTime previousPaymentTimeStamp, decimal previousPaymentAmount, string? currency, DateTime timeStamp,
        Guid accountId)
    {
        OpenBankingDirectDebitId = openBankingDirectDebitId;
        Name = name;
        Status = status;
        PreviousPaymentTimeStamp = previousPaymentTimeStamp;
        PreviousPaymentAmount = previousPaymentAmount;
        Currency = currency;
        TimeStamp = timeStamp;
        AccountId = accountId;
    }

    [Encrypt] public string OpenBankingDirectDebitId { get; private set; }

    [Encrypt] public string Name { get; private set; }

    [Encrypt] public string Status { get; private set; }

    [Encrypt] public DateTime PreviousPaymentTimeStamp { get; private set; }

    [Encrypt] public decimal PreviousPaymentAmount { get; private set; }

    [Encrypt] public string? Currency { get; private set; }

    [Encrypt] public DateTime TimeStamp { get; private set; }

    public Guid AccountId { get; private set; }
    public OpenBankingAccount? Account { get; private set; }
}
