using Bogus;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

namespace FinanceTracker.Tests.Unit.Shared.Fakers.TrueLayer;

public sealed class ExternalOpenBankingAccountBalanceFaker : Faker<ExternalOpenBankingAccountBalance>
{
    public ExternalOpenBankingAccountBalanceFaker()
    {
        RuleFor(x => x.Currency, "GBP");
        RuleFor(x => x.Current, f => f.Finance.Amount(-500, 5000));
        RuleFor(x => x.Available, (f, x) => x.Current - f.Finance.Amount(0, 100));
        RuleFor(x => x.UpdateTimestamp, f => f.Date.Recent(1, DateTime.UtcNow));
    }
}
