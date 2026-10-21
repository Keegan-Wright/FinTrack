using Bogus;
using FinanceTracker.Domain.Household;

namespace FinanceTracker.Tests.Unit.Shared.Fakers;

public sealed class HouseholdMemberFaker : Faker<HouseholdMember>
{
    public HouseholdMemberFaker(Guid userId) =>
        CustomInstantiator(f => new HouseholdMember(userId,
            f.Name.FirstName(),
            f.Name.LastName(),
            f.Random.Bool(0.8f) ? f.Finance.Amount(0, 5000) : null));
}
