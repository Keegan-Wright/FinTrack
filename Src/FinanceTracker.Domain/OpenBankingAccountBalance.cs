using FinanceTracker.Domain.Utility;

namespace FinanceTracker.Domain;

public class OpenBankingAccountBalance : BaseEntity
{
    [Encrypt]
    public required string Currency { get; init; }

    [Encrypt]
    public required decimal Available { get; set; }

    [Encrypt]
    public required decimal Current { get; set; }

    public Guid AccountId { get; init; }
    public OpenBankingAccount? Account { get; init; }
}
