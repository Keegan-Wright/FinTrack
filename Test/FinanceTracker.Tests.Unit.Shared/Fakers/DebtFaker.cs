using Bogus;
using FinanceTracker.Domain.Debts;

namespace FinanceTracker.Tests.Unit.Shared.Fakers;

public sealed class DebtFaker : Faker<Debt>
{
    public DebtFaker(Guid userId) =>
        CustomInstantiator(f => new Debt(userId,
            f.Finance.AccountName(),
            f.Company.CompanyName(),
            f.Finance.Amount(1000, 20000),
            DateOnly.FromDateTime(f.Date.Past(2, DateTime.UtcNow)),
            f.Random.Decimal(0, 25),
            f.Finance.Amount(50, 500)));
}
