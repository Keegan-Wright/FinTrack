using FinanceTracker.Domain.Utility;

namespace FinanceTracker.Domain;

public class OpenBankingStandingOrder : BaseEntity
{
    public OpenBankingStandingOrder(string frequency, string status, string currency, DateTime nextPaymentDate,
        decimal nextPaymentAmount, DateTime firstPaymentDate, decimal firstPaymentAmount, DateTime finalPaymentDate,
        decimal finalPaymentAmount, string reference, string payee, DateTime timestamp, Guid accountId)
    {
        Frequency = frequency;
        Status = status;
        Currency = currency;
        NextPaymentDate = nextPaymentDate;
        NextPaymentAmount = nextPaymentAmount;
        FirstPaymentDate = firstPaymentDate;
        FirstPaymentAmount = firstPaymentAmount;
        FinalPaymentDate = finalPaymentDate;
        FinalPaymentAmount = finalPaymentAmount;
        Reference = reference;
        Payee = payee;
        Timestamp = timestamp;
        AccountId = accountId;
    }

    [Encrypt] public string Frequency { get; private set; }

    [Encrypt] public string Status { get; private set; }

    [Encrypt] public string Currency { get; private set; }

    [Encrypt] public DateTime NextPaymentDate { get; private set; }

    [Encrypt] public decimal NextPaymentAmount { get; private set; }

    [Encrypt] public DateTime FirstPaymentDate { get; private set; }

    [Encrypt] public decimal FirstPaymentAmount { get; private set; }

    [Encrypt] public DateTime FinalPaymentDate { get; private set; }

    [Encrypt] public decimal FinalPaymentAmount { get; private set; }

    [Encrypt] public string Reference { get; private set; }

    [Encrypt] public string Payee { get; private set; }

    [Encrypt] public DateTime Timestamp { get; private set; }

    public Guid AccountId { get; private set; }
    public OpenBankingAccount? Account { get; private set; }
}
