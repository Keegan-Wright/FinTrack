using Bogus;
using FinanceTracker.Domain.OpenBanking.Accounts;

namespace FinanceTracker.Tests.Unit.Shared.Fakers;

public sealed class AccountBalanceFaker : Faker<AccountBalance>
{
    public AccountBalanceFaker() =>
        CustomInstantiator(f =>
        {
            decimal current = f.Finance.Amount(-500, 5000);
            return new AccountBalance(current - f.Finance.Amount(0, 100), current, f.Date.Recent(1, DateTime.UtcNow));
        });
}
