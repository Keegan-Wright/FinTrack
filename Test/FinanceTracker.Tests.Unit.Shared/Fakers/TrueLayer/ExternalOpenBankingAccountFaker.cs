using Bogus;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

namespace FinanceTracker.Tests.Unit.Shared.Fakers.TrueLayer;

public sealed class ExternalOpenBankingAccountFaker : Faker<ExternalOpenBankingAccount>
{
    public ExternalOpenBankingAccountFaker()
    {
        RuleFor(x => x.AccountId, f => $"acc-{f.UniqueIndex}-{f.Random.AlphaNumeric(6)}");
        RuleFor(x => x.AccountType, f => f.PickRandom("TRANSACTION", "SAVINGS"));
        RuleFor(x => x.DisplayName, f => f.Finance.AccountName());
        RuleFor(x => x.Currency, "GBP");
        RuleFor(x => x.UpdateTimestamp, _ => DateTime.UtcNow);
    }
}
