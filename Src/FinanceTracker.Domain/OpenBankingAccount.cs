using FinanceTracker.Domain.Utility;

namespace FinanceTracker.Domain;

public class OpenBankingAccount : BaseEntity
{
    public OpenBankingAccount(string openBankingAccountId, string accountType, string displayName, string currency,
        Guid providerId)
    {
        OpenBankingAccountId = openBankingAccountId;
        AccountType = accountType;
        DisplayName = displayName;
        Currency = currency;
        ProviderId = providerId;
    }

    public string OpenBankingAccountId { get; private set; }

    [Encrypt] public string AccountType { get; private set; }

    [Encrypt] public string DisplayName { get; private set; }

    [Encrypt] public string Currency { get; private set; }

    public Guid ProviderId { get; private set; }

    public OpenBankingProvider? Provider { get; private set; }

    public OpenBankingAccountBalance? AccountBalance { get; private set; }

    public ICollection<OpenBankingTransaction>? Transactions { get; private set; }
    public ICollection<OpenBankingStandingOrder>? StandingOrders { get; private set; }
    public ICollection<OpenBankingDirectDebit>? DirectDebits { get; private set; }

    public ICollection<OpenBankingSynchronization>? Syncronisations { get; private set; }
}
