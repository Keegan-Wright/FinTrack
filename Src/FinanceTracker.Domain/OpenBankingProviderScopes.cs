using FinanceTracker.Domain.Utility;

namespace FinanceTracker.Domain;

public class OpenBankingProviderScopes : BaseEntity
{
    public OpenBankingProviderScopes(string scope, Guid providerId)
    {
        Scope = scope;
        ProviderId = providerId;
    }

    [Encrypt] public string Scope { get; private set; }

    public Guid ProviderId { get; private set; }
    public OpenBankingProvider? Provider { get; private set; }
}
