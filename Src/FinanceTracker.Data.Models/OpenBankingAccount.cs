using FinanceTracker.Data.Models.Utility;

namespace FinanceTracker.Data.Models;

public class OpenBankingAccount : BaseEntity
{
    public required string OpenBankingAccountId { get; set; }
    [Encrypt]
    public required string AccountType { get; set; }
    
    [Encrypt]
    public required string DisplayName { get; set; }
    
    [Encrypt]
    public required string Currency { get; set; }

    public required Guid ProviderId { get; set; }

    public OpenBankingProvider Provider { get; set; }

    public OpenBankingAccountBalance? AccountBalance { get; set; }

    public ICollection<OpenBankingTransaction> Transactions { get; set; }
    public ICollection<OpenBankingStandingOrder> StandingOrders { get; set; }
    public ICollection<OpenBankingDirectDebit> DirectDebits { get; set; }

    public ICollection<OpenBankingSynchronization> Syncronisations { get; set; }
}