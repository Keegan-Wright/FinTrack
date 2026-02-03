namespace FinanceTracker.Data.Models;


public class OpenBankingProviderScopes : BaseEntity 
{ 
    public required string Scope { get; set; }
    
    public Guid ProviderId { get; set; } 
    public OpenBankingProvider Provider { get; set; }

}