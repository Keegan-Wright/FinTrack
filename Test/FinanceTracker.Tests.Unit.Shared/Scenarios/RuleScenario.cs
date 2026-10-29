using FinanceTracker.Domain.Rules;
using FinanceTracker.Tests.Unit.Shared.Fakers;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

public sealed class RuleScenario : ScenarioBuilder
{
    public RuleScenario(TestingScenario scenario) : base(scenario)
    {
    }

    /// <param name="build">Builds the rule from the user's id, generated when omitted.</param>
    public Task<TestingScenario> WithRule(string ruleName, string username, Func<Guid, Rule>? build = null)
    {
        Guid userId = Scenario.UserMappings[username];
        Rule rule = build is null ? new RuleFaker(userId).Generate() : build(userId);

        Scenario.RuleMappings.Add(ruleName, rule.Id);
        return PersistAsync(context => context.Rules.Add(rule));
    }
}
