using FinanceTracker.Domain.Classifications;
using FinanceTracker.Tests.Unit.Shared.Fakers;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

/// <summary>
/// Seeds tags, mapped by name in <see cref="TestingScenario.ClassificationMappings"/>.
/// </summary>
public sealed class ClassificationScenario : ScenarioBuilder
{
    /// <param name="scenario">The scenario it seeds.</param>
    public ClassificationScenario(TestingScenario scenario) : base(scenario)
    {
    }

    /// <summary>
    /// Adds a tag for the user.
    /// </summary>
    /// <param name="classificationName">The name to map the tag by.</param>
    /// <param name="username">The user who owns it, by their scenario name.</param>
    /// <param name="tag">Generated when omitted.</param>
    public Task<TestingScenario> WithClassification(string classificationName, string username, string? tag = null)
    {
        Guid userId = Scenario.UserMappings[username];
        CustomClassification classification = tag is null
            ? new CustomClassificationFaker(userId).Generate()
            : new CustomClassification(userId, tag);

        Scenario.ClassificationMappings.Add(classificationName, classification.Id);
        return PersistAsync(context => context.CustomClassifications.Add(classification));
    }
}
