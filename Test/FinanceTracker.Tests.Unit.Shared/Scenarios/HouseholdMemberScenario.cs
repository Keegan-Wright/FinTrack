using FinanceTracker.Domain.Household;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

/// <summary>
/// Seeds household members and how they're recognised, mapped by name in <see
/// cref="TestingScenario.HouseholdMemberMappings"/>.
/// </summary>
public sealed class HouseholdMemberScenario : ScenarioBuilder
{
    /// <param name="scenario">The scenario it seeds.</param>
    public HouseholdMemberScenario(TestingScenario scenario) : base(scenario)
    {
    }

    /// <summary>
    /// Adds a household member for the user.
    /// </summary>
    /// <param name="householdMemberName">The name to map the member by.</param>
    /// <param name="username">The user whose household they're in, by their scenario name.</param>
    /// <param name="create">Builds the member for the owning user id, generated when omitted.</param>
    public Task<TestingScenario> WithHouseholdMember(string householdMemberName, string username,
        Func<Guid, HouseholdMember>? create = null)
    {
        Guid userId = Scenario.UserMappings[username];
        HouseholdMember householdMember = create?.Invoke(userId) ?? new HouseholdMemberFaker(userId).Generate();

        Scenario.HouseholdMemberMappings.Add(householdMemberName, householdMember.Id);
        return PersistAsync(context => context.HouseholdMembers.Add(householdMember));
    }

    /// <summary>
    /// Adds a name the member is paid or pays as.
    /// </summary>
    /// <param name="householdMemberName">The member, by their scenario name.</param>
    /// <param name="payeeName">The name as it appears in transaction descriptions.</param>
    public Task<TestingScenario> WithPayeeName(string householdMemberName, string payeeName) =>
        PersistAsync(async context => (await LoadAsync(context, householdMemberName)).AddPayeeName(payeeName));

    /// <summary>
    /// Links one of the member's own accounts.
    /// </summary>
    /// <param name="householdMemberName">The member, by their scenario name.</param>
    /// <param name="sortCode">The account's sort code.</param>
    /// <param name="number">The account's number.</param>
    public Task<TestingScenario> WithLinkedAccount(string householdMemberName, string sortCode, string number) =>
        PersistAsync(async context => (await LoadAsync(context, householdMemberName))
            .LinkAccount(AccountIdentifier.Create(sortCode, number, null)!));

    /// <summary>
    /// Loads the member to change.
    /// </summary>
    /// <param name="context">The context to load it in.</param>
    /// <param name="householdMemberName">The member, by their scenario name.</param>
    private Task<HouseholdMember> LoadAsync(FinanceTrackerContext context, string householdMemberName)
    {
        Guid householdMemberId = Scenario.HouseholdMemberMappings[householdMemberName];
        return context.HouseholdMembers.SingleAsync(x => x.Id == householdMemberId, Scenario.CancellationToken);
    }
}
