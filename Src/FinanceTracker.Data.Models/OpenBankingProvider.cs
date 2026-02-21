using FinanceTracker.Data.Models.Utility;

namespace FinanceTracker.Data.Models;

public class OpenBankingProvider : BaseEntity
{
    
    [Encrypt]
    public required string Name { get; set; }
    
    [Encrypt]
    public required string AccessCode { get; set; }
    
    [Encrypt]
    public required string OpenBankingProviderId { get; set; }
    
    [Encrypt]
    public required byte[] Logo { get; set; }

    public ICollection<OpenBankingProviderScopes> Scopes { get; set; }
    public ICollection<OpenBankingAccount> Accounts { get; set; }
    public ICollection<OpenBankingTransaction> Transactions { get; set; }
    public ICollection<OpenBankingSynchronization> Syncronisations { get; set; }

}