using System.Collections.Specialized;
using FinanceTracker.Infrastructure.Automation;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Automation;

/// <summary>
/// The app declares each job's starting schedule to Quartz on every start, with duplicates ignored. Against a
/// persistent store, as the app's Postgres one, a restart keeps a schedule changed since rather than putting the
/// starting one back.
/// </summary>
public class DeclaredScheduleTests : UnitTestBase
{
    private const string Function = "SyncAllOpenBankingDetailsAsync";

    [Test]
    public async Task Restart_AfterTheScheduleWasChanged_KeepsTheChange()
    {
        // Arrange
        string database = Path.Combine(Path.GetTempPath(), $"quartz-{Guid.NewGuid()}.db");
        string scheduler = $"declared-{Guid.NewGuid()}";
        try
        {
            await using (ServiceProvider first = Start(database, scheduler))
            {
                IScheduler started = await StartedAsync(first);
                await started.RescheduleJob(JobSchedules.KeyFor(Function),
                    JobSchedules.Build(Function, "Changed", "0 0 */2 * * ?", 0, []), CancellationToken);
                await started.Shutdown(waitForJobsToComplete: true, CancellationToken);
            }

            // Act
            await using ServiceProvider second = Start(database, scheduler);
            IScheduler restarted = await StartedAsync(second);
            var schedule = (ICronTrigger?)await restarted.GetTrigger(JobSchedules.KeyFor(Function), CancellationToken);
            await restarted.Shutdown(waitForJobsToComplete: true, CancellationToken);

            // Assert
            using (Assert.Multiple())
            {
                await Assert.That(schedule!.CronExpressionString).IsEqualTo("0 0 */2 * * ?");
                await Assert.That(schedule.Description).IsEqualTo("Changed");
            }
        }
        finally
        {
            File.Delete(database);
        }
    }

    /// <summary>
    /// Quartz as the app sets it up, its schedule declared the same way, on a SQLite file in place of Postgres.
    /// </summary>
    private static ServiceProvider Start(string database, string scheduler)
    {
        ServiceCollection services = new();
        services.AddLogging();
        NameValueCollection properties = new() { ["quartz.scheduler.instanceName"] = scheduler };
        services.AddQuartz(properties, quartz =>
        {
            quartz.UsePersistentStore(store =>
            {
                store.UseSqlite($"Data Source={database}");
                store.ProvisionSchema();
            });
            quartz.AddJob<RecordingJob>(static job => job.WithIdentity(Function).StoreDurably());
            quartz.AddTrigger(static trigger => JobSchedules.Configure(trigger, Function, "Starting",
                "0 0 */4 * * ?", 1, [10]));
        });
        services.Configure<QuartzOptions>(static options => options.Scheduling.IgnoreDuplicates = true);
        return services.BuildServiceProvider();
    }

    private async Task<IScheduler> StartedAsync(ServiceProvider provider)
    {
        IScheduler scheduler = await provider.GetRequiredService<ISchedulerFactory>().GetScheduler(CancellationToken);
        await scheduler.Start(CancellationToken);
        return scheduler;
    }
}
