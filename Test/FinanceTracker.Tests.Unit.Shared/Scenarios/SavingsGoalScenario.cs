using FinanceTracker.Domain.SavingsGoals;
using FinanceTracker.Tests.Unit.Shared.Fakers;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

/// <summary>
/// Seeds savings goals, mapped by name in <see cref="TestingScenario.SavingsGoalMappings"/>.
/// </summary>
public sealed class SavingsGoalScenario : ScenarioBuilder
{
    /// <param name="scenario">The scenario it seeds.</param>
    public SavingsGoalScenario(TestingScenario scenario) : base(scenario)
    {
    }

    /// <summary>
    /// Adds a savings goal for the user.
    /// </summary>
    /// <param name="goalName">The name to map the goal by.</param>
    /// <param name="username">The user who owns it, by their scenario name.</param>
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
