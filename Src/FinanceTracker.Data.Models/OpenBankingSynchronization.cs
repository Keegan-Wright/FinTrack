using FinanceTracker.Data.Models.Utility;

namespace FinanceTracker.Data.Models;

public class OpenBankingSynchronization : BaseEntity
{
    public required int SyncronisationType { get; init; }

    public required DateTime SyncronisationTime { get; init; }


    public Guid ProviderId { get; init; }
    public OpenBankingProvider? Provider { get; init; }

    public Guid AccountId { get; set; }
    public OpenBankingAccount? Account { get; set; }


    [Encrypt]
    public required string OpenBankingAccountId { get; init; }
}
