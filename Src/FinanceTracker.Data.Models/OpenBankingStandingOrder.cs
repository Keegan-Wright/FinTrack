using FinanceTracker.Data.Models.Utility;

namespace FinanceTracker.Data.Models;

public class OpenBankingStandingOrder : BaseEntity
{
    [Encrypt]
    public required string Frequency { get; set; }

    [Encrypt]
    public required string Status { get; set; }

    [Encrypt]
    public required string Currency { get; set; }

    [Encrypt]
    public required DateTime NextPaymentDate { get; set; }

    [Encrypt]
    public required decimal NextPaymentAmount { get; set; }

    [Encrypt]
    public required DateTime FirstPaymentDate { get; set; }

    [Encrypt]
    public required decimal FirstPaymentAmount { get; set; }

    [Encrypt]
    public required DateTime FinalPaymentDate { get; set; }

    [Encrypt]
    public required decimal FinalPaymentAmount { get; set; }

    [Encrypt]
    public required string Reference { get; set; }

    [Encrypt]
    public required string Payee { get; set; }

    [Encrypt]
    public required DateTime Timestamp { get; set; }

    public Guid AccountId { get; set; }
    public OpenBankingAccount Account { get; set; }
}
