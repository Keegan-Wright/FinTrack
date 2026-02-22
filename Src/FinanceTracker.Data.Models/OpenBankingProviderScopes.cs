using FinanceTracker.Data.Models.Utility;

namespace FinanceTracker.Data.Models;

public class OpenBankingProviderScopes : BaseEntity
{
    [Encrypt]
    public required string Scope { get; set; }

    public Guid ProviderId { get; set; }
    public OpenBankingProvider Provider { get; set; }
}
