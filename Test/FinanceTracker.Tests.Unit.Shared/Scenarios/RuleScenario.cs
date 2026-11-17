using FinanceTracker.Domain.Rules;
using FinanceTracker.Tests.Unit.Shared.Fakers;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

/// <summary>
/// Seeds rules, mapped by name in <see cref="TestingScenario.RuleMappings"/>.
/// </summary>
public sealed class RuleScenario : ScenarioBuilder
{
    /// <param name="scenario">The scenario it seeds.</param>
    public RuleScenario(TestingScenario scenario) : base(scenario)
    {
    }

    /// <summary>
    /// Adds a rule for the user.
    /// </summary>
    /// <param name="ruleName">The name to map the rule by.</param>
    /// <param name="username">The user who owns it, by their scenario name.</param>
    /// <param name="build">Builds the rule from the user's id, generated when omitted.</param>
    public Task<TestingScenario> WithRule(string ruleName, string username, Func<Guid, Rule>? build = null)
    {
        Guid userId = Scenario.UserMappings[username];
        Rule rule = build is null ? new RuleFaker(userId).Generate() : build(userId);

        Scenario.RuleMappings.Add(ruleName, rule.Id);
        return PersistAsync(context => context.Rules.Add(rule));
    }
}
