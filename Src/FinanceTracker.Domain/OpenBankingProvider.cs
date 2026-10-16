using FinanceTracker.Domain.Utility;

namespace FinanceTracker.Domain;

public class OpenBankingProvider : BaseEntity
{
    public OpenBankingProvider(string name, string accessCode, string openBankingProviderId, byte[] logo)
    {
        Name = name;
        AccessCode = accessCode;
        OpenBankingProviderId = openBankingProviderId;
        Logo = logo;
    }

    [Encrypt] public string Name { get; private set; }

    [Encrypt] public string AccessCode { get; private set; }

    [Encrypt] public string OpenBankingProviderId { get; private set; }

    [Encrypt] public byte[] Logo { get; private set; }

    public ICollection<OpenBankingProviderScopes>? Scopes { get; private set; }
    public ICollection<OpenBankingAccount>? Accounts { get; private set; }
    public ICollection<OpenBankingTransaction>? Transactions { get; private set; }
    public ICollection<OpenBankingSynchronization>? Syncronisations { get; private set; }
}
