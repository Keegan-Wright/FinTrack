using FinanceTracker.Infrastructure;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

public abstract class ScenarioBuilder
{
    protected TestingScenario Scenario { get; init; }

    public ScenarioBuilder(TestingScenario scenario)
    {
        Scenario = scenario;
    }

    /// <summary>
    /// Applies <paramref name="change"/> in its own context and saves it.
    /// </summary>
    protected async Task<TestingScenario> PersistAsync(Func<FinanceTrackerContext, Task> change)
    {
        await using var context = await Scenario.DbContextFactory.CreateDbContextAsync(Scenario.CancellationToken);

        await change(context);

        await context.SaveChangesAsync(Scenario.CancellationToken);

        return Scenario;
    }

    protected Task<TestingScenario> PersistAsync(Action<FinanceTrackerContext> change) =>
        PersistAsync(context =>
        {
            change(context);
            return Task.CompletedTask;
        });
}
