namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

public abstract class ScenarioBuilder
{
    protected TestingScenario Scenario { get; init; }

    public ScenarioBuilder(TestingScenario scenario)
    {
        Scenario = scenario;
    }
}
