using Bogus;
using FinanceTracker.Domain.OpenBanking.Accounts;

namespace FinanceTracker.Tests.Unit.Shared.Fakers;

public sealed class DirectDebitDetailsFaker : Faker<DirectDebitDetails>
{
    public DirectDebitDetailsFaker() =>
        CustomInstantiator(f => new DirectDebitDetails(
            $"dd-{f.UniqueIndex}-{f.Random.AlphaNumeric(6)}",
            f.Company.CompanyName(),
            "Active",
            f.Date.Recent(20, DateTime.UtcNow),
            f.Finance.Amount(5, 200),
            "GBP",
            DateTime.UtcNow));
}
