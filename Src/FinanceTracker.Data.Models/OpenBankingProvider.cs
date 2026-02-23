using FinanceTracker.Data.Models.Utility;

namespace FinanceTracker.Data.Models;

public class OpenBankingProvider : BaseEntity
{
    [Encrypt]
    public required string Name { get; init; }

    [Encrypt]
    public required string AccessCode { get; init; }

    [Encrypt]
    public required string OpenBankingProviderId { get; init; }

    [Encrypt]
    public required byte[] Logo { get; init; }

    public ICollection<OpenBankingProviderScopes>? Scopes { get; set; }
    public ICollection<OpenBankingAccount>? Accounts { get; set; }
    public ICollection<OpenBankingTransaction>? Transactions { get; set; }
    public ICollection<OpenBankingSynchronization>? Syncronisations { get; set; }
}
