using FinanceTracker.Domain.Household;
using FinanceTracker.Tests.Unit.Shared.Fakers;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

public sealed class HouseholdMemberScenario : ScenarioBuilder
{
    public HouseholdMemberScenario(TestingScenario scenario) : base(scenario)
    {
    }

    /// <param name="create">Builds the member for the owning user id, generated when omitted.</param>
    public Task<TestingScenario> WithHouseholdMember(string householdMemberName, string username,
        Func<Guid, HouseholdMember>? create = null)
    {
        Guid userId = Scenario.UserMappings[username];
        HouseholdMember householdMember = create?.Invoke(userId) ?? new HouseholdMemberFaker(userId).Generate();

        Scenario.HouseholdMemberMappings.Add(householdMemberName, householdMember.Id);
        return PersistAsync(context => context.HouseholdMembers.Add(householdMember));
    }
}
