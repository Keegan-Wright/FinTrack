using Bogus;
using FinanceTracker.Domain.OpenBanking.Accounts;

namespace FinanceTracker.Tests.Unit.Shared.Fakers;

public sealed class BankAccountFaker : Faker<BankAccount>
{
    public BankAccountFaker(Guid userId, Guid connectionId) =>
        CustomInstantiator(f => new BankAccount(userId, connectionId,
            $"acc-{f.UniqueIndex}-{f.Random.AlphaNumeric(6)}",
            f.PickRandom("TRANSACTION", "SAVINGS"),
            f.Finance.AccountName(),
            "GBP"));
}
