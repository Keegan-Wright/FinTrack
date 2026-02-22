using FinanceTracker.Data.Models.Utility;

namespace FinanceTracker.Data.Models;

public class OpenBankingSynchronization : BaseEntity
{
    public required int SyncronisationType { get; set; }

    public required DateTime SyncronisationTime { get; set; }


    public Guid ProviderId { get; set; }
    public OpenBankingProvider Provider { get; set; }

    public Guid AccountId { get; set; }
    public OpenBankingAccount Account { get; set; }


    [Encrypt]
    public string OpenBankingAccountId { get; set; }
}
