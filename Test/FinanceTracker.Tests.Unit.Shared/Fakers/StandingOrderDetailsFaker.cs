using Bogus;
using FinanceTracker.Domain.OpenBanking.Accounts;

namespace FinanceTracker.Tests.Unit.Shared.Fakers;

public sealed class StandingOrderDetailsFaker : Faker<StandingOrderDetails>
{
    public StandingOrderDetailsFaker() =>
        CustomInstantiator(f =>
        {
            decimal amount = f.Finance.Amount(5, 500);
            DateTime firstPayment = f.Date.Past(1, DateTime.UtcNow);

            return new StandingOrderDetails(
                $"REF-{f.UniqueIndex}-{f.Random.AlphaNumeric(6)}",
                f.Name.FullName(),
                "Monthly",
                "Active",
                "GBP",
                f.Date.Soon(28, DateTime.UtcNow),
                amount,
                firstPayment,
                amount,
                f.Date.Future(2, DateTime.UtcNow),
                amount,
                DateTime.UtcNow);
        });
}
