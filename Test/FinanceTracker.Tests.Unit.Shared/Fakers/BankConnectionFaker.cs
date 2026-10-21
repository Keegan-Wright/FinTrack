using Bogus;
using FinanceTracker.Domain.OpenBanking.Connections;

namespace FinanceTracker.Tests.Unit.Shared.Fakers;

public sealed class BankConnectionFaker : Faker<BankConnection>
{
    public static readonly string[] Scopes = ["info", "accounts", "balance", "transactions", "direct_debits", "standing_orders"];

    public BankConnectionFaker(Guid userId) =>
        CustomInstantiator(f => new BankConnection(userId,
            $"ob-{f.UniqueIndex}-{f.Random.AlphaNumeric(6)}",
            f.Company.CompanyName(),
            f.Random.AlphaNumeric(32),
            Scopes,
            f.Random.Bytes(16)));
}
