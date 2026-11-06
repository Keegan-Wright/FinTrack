using Bogus;
using FinanceTracker.Domain.SavingsGoals;

namespace FinanceTracker.Tests.Unit.Shared.Fakers;

/// <summary>
/// A goal with a name of its own, as a user's goal names are unique and there are only so many departments.
/// </summary>
public sealed class SavingsGoalFaker : Faker<SavingsGoal>
{
    public SavingsGoalFaker(Guid userId) =>
        CustomInstantiator(f => new SavingsGoal(userId,
            $"{f.Commerce.Department()}-{f.UniqueIndex}",
            f.Finance.Amount(0, 1000),
            f.Finance.Amount(0, 500),
            f.Finance.Amount(0, 10000),
            f.Date.Future(1, DateTime.UtcNow)));
}
