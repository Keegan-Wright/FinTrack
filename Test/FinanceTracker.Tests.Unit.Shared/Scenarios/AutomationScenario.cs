using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Enums;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

/// <summary>
/// Seeds TickerQ's job tables.
/// </summary>
public sealed class AutomationScenario : ScenarioBuilder
{
    public AutomationScenario(TestingScenario scenario) : base(scenario)
    {
    }

    /// <param name="configure">Adjusts the job before it is saved.</param>
    public Task<TestingScenario> WithCronJob(string cronName, string function,
        Action<CronTickerEntity>? configure = null)
    {
        CronTickerEntity cron = new()
        {
            Id = Guid.NewGuid(),
            Function = function,
            Description = $"{function} description",
            Expression = "0 * * * *",
            Retries = 1,
            RetryIntervals = [30],
            IsEnabled = true
        };
        configure?.Invoke(cron);

        Scenario.CronTickerMappings.Add(cronName, cron.Id);
        return PersistAsync(context => context.CronTickerEntities.Add(cron));
    }

    public Task<TestingScenario> WithCronOccurrence(string cronName, TickerStatus status, DateTime executionTime) =>
        PersistAsync(context => context.CronTickerOccurrenceEntities.Add(new CronTickerOccurrenceEntity<CronTickerEntity>
        {
            Id = Guid.NewGuid(),
            CronTickerId = Scenario.CronTickerMappings[cronName],
            Status = status,
            ExecutionTime = executionTime
        }));

    /// <summary>
    /// A one off, manually triggered run of <paramref name="function"/>.
    /// </summary>
    public Task<TestingScenario> WithTimeTicker(string function, TickerStatus status, DateTime? executionTime) =>
        PersistAsync(context =>
        {
            var ticker = context.TimeTickerEntities.Add(new TimeTickerEntity
            {
                Id = Guid.NewGuid(),
                Function = function,
                ExecutionTime = executionTime
            });

            // Status has an internal setter, only TickerQ moves it on
            ticker.Property(x => x.Status).CurrentValue = status;
        });
}
