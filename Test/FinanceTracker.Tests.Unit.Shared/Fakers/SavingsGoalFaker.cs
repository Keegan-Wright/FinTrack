using Bogus;
using FinanceTracker.Domain.SavingsGoals;

namespace FinanceTracker.Tests.Unit.Shared.Fakers;

public sealed class SavingsGoalFaker : Faker<SavingsGoal>
{
    public SavingsGoalFaker(Guid userId) =>
        CustomInstantiator(f => new SavingsGoal(userId,
            f.Commerce.Department(),
            f.Finance.Amount(0, 1000),
            f.Finance.Amount(0, 500),
            f.Finance.Amount(0, 10000),
            f.Date.Future(1, DateTime.UtcNow)));
}
