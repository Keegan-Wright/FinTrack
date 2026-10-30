using FinanceTracker.Domain.SavingsGoals;
using FinanceTracker.Tests.Unit.Shared.Fakers;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

public sealed class SavingsGoalScenario : ScenarioBuilder
{
    public SavingsGoalScenario(TestingScenario scenario) : base(scenario)
    {
    }

    /// <param name="create">Builds the goal for the owning user id, generated when omitted.</param>
    public Task<TestingScenario> WithSavingsGoal(string goalName, string username,
        Func<Guid, SavingsGoal>? create = null)
    {
        Guid userId = Scenario.UserMappings[username];
        SavingsGoal savingsGoal = create?.Invoke(userId) ?? new SavingsGoalFaker(userId).Generate();

        Scenario.SavingsGoalMappings.Add(goalName, savingsGoal.Id);
        return PersistAsync(context => context.SavingsGoals.Add(savingsGoal));
    }
}
