using FinanceTracker.Domain.Classifications;
using FinanceTracker.Tests.Unit.Shared.Fakers;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

public sealed class ClassificationScenario : ScenarioBuilder
{
    public ClassificationScenario(TestingScenario scenario) : base(scenario)
    {
    }

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
