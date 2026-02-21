using FinanceTracker.Data.Models.Utility;

namespace FinanceTracker.Data.Models;

public class OpenBankingAccountBalance : BaseEntity
{
    [Encrypt]
    public required string Currency { get; set; }
    
    [Encrypt]
    public required decimal Available { get; set; }
    
    [Encrypt]
    public required decimal Current { get; set; }
    
    public Guid AccountId { get; set; }
    public OpenBankingAccount Account { get; set; }
}