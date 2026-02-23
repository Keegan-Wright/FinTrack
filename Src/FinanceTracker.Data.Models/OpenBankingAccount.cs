using FinanceTracker.Data.Models.Utility;

namespace FinanceTracker.Data.Models;

public class OpenBankingAccount : BaseEntity
{
    public required string OpenBankingAccountId { get; init; }

    [Encrypt]
    public required string AccountType { get; init; }

    [Encrypt]
    public required string DisplayName { get; init; }

    [Encrypt]
    public required string Currency { get; init; }

    public required Guid ProviderId { get; init; }

    public OpenBankingProvider? Provider { get; init; }

    public OpenBankingAccountBalance? AccountBalance { get; set; }

    public ICollection<OpenBankingTransaction>? Transactions { get; set; }
    public ICollection<OpenBankingStandingOrder>? StandingOrders { get; set; }
    public ICollection<OpenBankingDirectDebit>? DirectDebits { get; set; }

    public ICollection<OpenBankingSynchronization>? Syncronisations { get; init; }
}
