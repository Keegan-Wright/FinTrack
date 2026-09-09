using FinanceTracker.Domain.Utility;

namespace FinanceTracker.Domain;

public class OpenBankingProviderScopes : BaseEntity
{
    [Encrypt]
    public required string Scope { get; init; }

    public Guid ProviderId { get; init; }
    public OpenBankingProvider? Provider { get; init; }
}
