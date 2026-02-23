using FinanceTracker.Data.Models.Utility;

namespace FinanceTracker.Data.Models;

public class OpenBankingProviderScopes : BaseEntity
{
    [Encrypt]
    public required string Scope { get; init; }

    public Guid ProviderId { get; init; }
    public OpenBankingProvider? Provider { get; init; }
}
