using FinanceTracker.Domain.Utility;

namespace FinanceTracker.Domain;

public class OpenBankingSynchronization : BaseEntity
{
    public OpenBankingSynchronization(int synchronisationType, string openBankingAccountId,
        DateTime synchronisationTime, Guid providerId, Guid accountId)
    {
        SynchronisationType = synchronisationType;
        SynchronisationTime = synchronisationTime;
        ProviderId = providerId;
        AccountId = accountId;
        OpenBankingAccountId = openBankingAccountId;
    }

    public int SynchronisationType { get; private set; }

    public DateTime SynchronisationTime { get; private set; }


    public Guid ProviderId { get; private set; }
    public OpenBankingProvider? Provider { get; private set; }

    public Guid AccountId { get; private set; }
    public OpenBankingAccount? Account { get; private set; }


    [Encrypt] public string OpenBankingAccountId { get; private set; }
}
