using FinanceTracker.Domain.Utility;

namespace FinanceTracker.Domain;

public class OpenBankingAccountBalance : BaseEntity
{
    public OpenBankingAccountBalance(string currency, decimal available, decimal current, Guid accountId)
    {
        Currency = currency;
        Available = available;
        Current = current;
        AccountId = accountId;
    }

    [Encrypt] public string Currency { get; private set; }

    [Encrypt] public decimal Available { get; private set; }

    [Encrypt] public decimal Current { get; private set; }

    public Guid AccountId { get; private set; }
    public OpenBankingAccount? Account { get; private set; }
}
