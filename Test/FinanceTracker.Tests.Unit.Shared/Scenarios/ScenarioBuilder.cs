using FinanceTracker.Infrastructure;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

/// <summary>
/// The base of each part of a <see cref="TestingScenario"/>. Each step saves in its own context and returns the
/// scenario, so steps chain.
/// </summary>
public abstract class ScenarioBuilder
{
    /// <summary>
    /// The scenario it seeds, with the names already mapped.
    /// </summary>
    protected TestingScenario Scenario { get; init; }

    /// <param name="scenario">The scenario it seeds.</param>
    public ScenarioBuilder(TestingScenario scenario)
    {
        Scenario = scenario;
    }

    /// <summary>
    /// Applies <paramref name="change"/> in its own context and saves it.
    /// </summary>
    /// <param name="change">What to change.</param>
    protected async Task<TestingScenario> PersistAsync(Func<FinanceTrackerContext, Task> change)
    {
        await using FinanceTrackerContext context =
            await Scenario.DbContextFactory.CreateDbContextAsync(Scenario.CancellationToken);

        await change(context);

        await context.SaveChangesAsync(Scenario.CancellationToken);

        return Scenario;
    }

    /// <summary>
    /// Applies <paramref name="change"/> in its own context and saves it.
    /// </summary>
    /// <param name="change">What to change.</param>
    protected Task<TestingScenario> PersistAsync(Action<FinanceTrackerContext> change) =>
        PersistAsync(context =>
        {
            change(context);
            return Task.CompletedTask;
        });
}
