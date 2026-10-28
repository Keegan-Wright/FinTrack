using FinanceTracker.Domain.Household;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using Microsoft.EntityFrameworkCore;

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

    public Task<TestingScenario> WithPayeeName(string householdMemberName, string payeeName) =>
        PersistAsync(async context => (await LoadAsync(context, householdMemberName)).AddPayeeName(payeeName));

    public Task<TestingScenario> WithLinkedAccount(string householdMemberName, string sortCode, string number) =>
        PersistAsync(async context => (await LoadAsync(context, householdMemberName))
            .LinkAccount(AccountIdentifier.Create(sortCode, number, null)!));

    private Task<HouseholdMember> LoadAsync(FinanceTrackerContext context, string householdMemberName)
    {
        Guid householdMemberId = Scenario.HouseholdMemberMappings[householdMemberName];
        return context.HouseholdMembers.SingleAsync(x => x.Id == householdMemberId, Scenario.CancellationToken);
    }
}
