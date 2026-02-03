namespace FinanceTracker.Data.Models;

public class OpenBankingProvider : BaseEntity
{
    public required string Name { get; set; }
    public required string AccessCode { get; set; }
    public required string OpenBankingProviderId { get; set; }
    public required byte[] Logo { get; set; }

    public ICollection<OpenBankingProviderScopes> Scopes { get; set; }
    public ICollection<OpenBankingAccount> Accounts { get; set; }
    public ICollection<OpenBankingTransaction> Transactions { get; set; }
    public ICollection<OpenBankingSynchronization> Syncronisations { get; set; }

}