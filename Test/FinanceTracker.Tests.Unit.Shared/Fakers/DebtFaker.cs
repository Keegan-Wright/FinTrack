using Bogus;
using FinanceTracker.Domain.Budgeting;

namespace FinanceTracker.Tests.Unit.Shared.Fakers;

public sealed class DebtFaker : Faker<Debt>
{
    public DebtFaker(Guid userId) =>
        CustomInstantiator(f => new Debt(userId,
            f.Finance.AccountName(),
            f.Finance.Amount(10, 500),
            f.Date.Future(5, DateTime.UtcNow),
            f.Date.Future(3, DateTime.UtcNow)));
}
