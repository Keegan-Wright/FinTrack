using FinanceTracker.Data.Models.Utility;

namespace FinanceTracker.Data.Models;

public class OpenBankingDirectDebit : BaseEntity
{
    [Encrypt]
    public required string OpenBankingDirectDebitId { get; set; }

    [Encrypt]
    public required string Name { get; set; }

    [Encrypt]
    public required string Status { get; set; }

    [Encrypt]
    public required DateTime PreviousPaymentTimeStamp { get; set; }

    [Encrypt]
    public required decimal PreviousPaymentAmount { get; set; }

    [Encrypt]
    public required string? Currency { get; set; }

    [Encrypt]
    public required DateTime TimeStamp { get; set; }

    public Guid AccountId { get; set; }
    public OpenBankingAccount Account { get; set; }
}
