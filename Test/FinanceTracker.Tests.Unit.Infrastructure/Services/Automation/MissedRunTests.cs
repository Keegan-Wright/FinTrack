using System.Collections.Specialized;
using FinanceTracker.Infrastructure.Automation;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Automation;

/// <summary>
/// The app switched off with its schedule stored, then started again, against a persistent store as the app's
/// Postgres one. Quartz runs on a clock set back while the schedule is stored, then on the real one, so it comes back
/// already overdue, as after the machine was off. It catches up with one run, then keeps to its cron.
/// </summary>
public class MissedRunTests : UnitTestBase
{
    private const string Function = "SyncAllOpenBankingDetailsAsync";
    private const string EveryFourHours = "0 0 */4 * * ?";

    /// <summary>
    /// Long enough for a second catch-up run to have happened, were there going to be one per missed run.
    /// </summary>
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(2);

    [Test]
    public async Task Start_AfterThreeDaysOff_RunsOnceThenKeepsToTheCron()
    {
        // Arrange: stored three days ago, so 18 four-hourly runs were missed
        await using QuartzOnFile quartz = new();
        await quartz.StoreAsync(DateTime.UtcNow.AddDays(-3), CancellationToken);
        IScheduler scheduler = await quartz.RestartAsync(CancellationToken);

        // Act
        await scheduler.Start(CancellationToken);
        await WaitForRunAsync(quartz.Name);
        await Task.Delay(Settle, CancellationToken);

        // Assert
        ITrigger? schedule = await scheduler.GetTrigger(JobSchedules.KeyFor(Function), CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(CountingJob.Runs(quartz.Name)).IsEqualTo(1);
            await AssertOnTheCronAsync(schedule!.NextFireTimeUtc);
        }
    }

    private async Task WaitForRunAsync(string scheduler)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        timeout.CancelAfter(TestTimeouts.Wait);
        while (CountingJob.Runs(scheduler) == 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        }
    }

    /// <summary>
    /// The next run is the next four-hourly slot from now, not four hours from the catch-up run.
    /// </summary>
    private static async Task AssertOnTheCronAsync(DateTimeOffset? next)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await Assert.That(next).IsNotNull();
        DateTimeOffset due = next!.Value;
        await Assert.That(due).IsGreaterThan(now);
        await Assert.That(due).IsLessThanOrEqualTo(now.AddHours(4));
        await Assert.That(due.Hour % 4).IsEqualTo(0);
        await Assert.That(due.Minute).IsEqualTo(0);
        await Assert.That(due.Second).IsEqualTo(0);
    }

    /// <summary>
    /// Quartz as the app sets it up, its schedule declared the same way, on its own SQLite file in place of Postgres.
    /// A schedule stored this way is due from the clock it was stored on, where one built by hand and rescheduled is
    /// due from now.
    /// </summary>
    private sealed class QuartzOnFile : IAsyncDisposable
    {
        private readonly string _database = Path.Combine(Path.GetTempPath(), $"quartz-{Guid.NewGuid()}.db");
        private ServiceProvider? _running;

        public string Name { get; } = $"missed-{Guid.NewGuid()}";

        /// <summary>
        /// Stores the schedule as the app would declaring it at <paramref name="storedAt"/>, without starting the
        /// scheduler, then shuts down.
        /// </summary>
        public async Task StoreAsync(DateTime storedAt, CancellationToken cancellationToken)
        {
            await using ServiceProvider then = Build(new FixedTimeProvider(storedAt));
            IScheduler scheduler = await SchedulerOf(then, cancellationToken);
            await scheduler.Shutdown(waitForJobsToComplete: true, cancellationToken);
        }

        /// <summary>
        /// Quartz on the real clock, the stored schedule winning over the one it declares again. Not started, as the
        /// app is before its scheduler starts.
        /// </summary>
        public Task<IScheduler> RestartAsync(CancellationToken cancellationToken)
        {
            _running = Build(TimeProvider.System);
            return SchedulerOf(_running, cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            if (_running is not null)
            {
                IScheduler scheduler = await SchedulerOf(_running, CancellationToken.None);
                await scheduler.Shutdown(waitForJobsToComplete: true);
                await _running.DisposeAsync();
            }

            File.Delete(_database);
        }

        private static Task<IScheduler> SchedulerOf(ServiceProvider provider, CancellationToken cancellationToken) =>
            provider.GetRequiredService<ISchedulerFactory>().GetScheduler(cancellationToken).AsTask();

        private ServiceProvider Build(TimeProvider clock)
        {
            ServiceCollection services = new();
            services.AddLogging();
            services.AddSingleton(clock);
            NameValueCollection properties = new() { ["quartz.scheduler.instanceName"] = Name };
            services.AddQuartz(properties, quartz =>
            {
                quartz.UsePersistentStore(store =>
                {
                    store.UseSqlite($"Data Source={_database}");
                    store.ProvisionSchema();
                });
                quartz.AddJob<CountingJob>(static job => job.WithIdentity(Function).StoreDurably());
                quartz.AddTrigger(static trigger => JobSchedules.Configure(trigger, Function, "Starting",
                    EveryFourHours, 0, []));
            });
            services.Configure<QuartzOptions>(static options => options.Scheduling.IgnoreDuplicates = true);
            return services.BuildServiceProvider();
        }
    }
}
