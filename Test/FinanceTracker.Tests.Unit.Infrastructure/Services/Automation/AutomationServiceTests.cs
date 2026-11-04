using System.Collections.Specialized;
using FinanceTracker.Contracts.Automation;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Infrastructure.Automation;
using FinanceTracker.Infrastructure.OpenBanking;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Quartz;
using Quartz.Extensibility;
using TUnit.Assertions.Enums;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Automation;

/// <summary>
/// Against a real Quartz scheduler, in memory, with its in-memory execution history, which behave as the Postgres
/// store and history the app uses.
/// </summary>
public class AutomationServiceTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";

    private static readonly DateTime Now = new(2026, 3, 10, 12, 0, 30, DateTimeKind.Utc);

    private readonly IOpenBankingSyncScheduler _syncScheduler = Substitute.For<IOpenBankingSyncScheduler>();

    // Local time is UTC, so the times read back as stored
    private readonly FixedTimeProvider _time = new(Now, TimeZoneInfo.Utc);

    private ServiceProvider? _quartz;

    [Test]
    public async Task GetJobsAsync_ReturnsTheSchedules()
    {
        // Arrange
        IScheduler scheduler = await SchedulerAsync();
        await ScheduleAsync(scheduler, JobNames.SyncAll, "0 */5 * * * ?", retries: 3, intervals: [10, 20, 30]);
        await ScheduleAsync(scheduler, JobNames.MonitorTokens, "0 0 */12 * * ?", retries: 0, intervals: []);
        await scheduler.PauseTrigger(JobSchedules.KeyFor(JobNames.SyncAll), CancellationToken);

        // Act
        List<AutomationCron> jobs = await BuildSubject().GetJobsAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert
        AutomationCron sync = jobs.Single(x => x.Function == JobNames.SyncAll);
        using (Assert.Multiple())
        {
            await Assert.That(jobs.Select(static x => x.Function))
                .IsEquivalentTo([JobNames.MonitorTokens, JobNames.SyncAll], CollectionOrdering.Matching);
            await Assert.That(sync.Description).IsEqualTo($"{JobNames.SyncAll} description");
            await Assert.That(sync.Expression).IsEqualTo("0 */5 * * * ?");
            await Assert.That(sync.Retries).IsEqualTo(3);
            await Assert.That(sync.RetryIntervals).IsEquivalentTo([10, 20, 30], CollectionOrdering.Matching);
            await Assert.That(sync.IsEnabled).IsFalse();
            await Assert.That(jobs.Single(x => x.Function == JobNames.MonitorTokens).Retries).IsEqualTo(0);
        }
    }

    [Test]
    public async Task GetJobsAsync_WhenNoJobsAreScheduled_ReturnsEmpty()
    {
        // Arrange
        await SchedulerAsync();

        // Act
        List<AutomationCron> jobs = await BuildSubject().GetJobsAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert
        await Assert.That(jobs).IsEmpty();
    }

    [Test]
    public async Task UpdateJobSettingsAsync_ReschedulesThePausedJob()
    {
        // Arrange
        IScheduler scheduler = await SchedulerAsync();
        await ScheduleAsync(scheduler, JobNames.SyncAll, "0 0 */4 * * ?", retries: 1, intervals: [10]);

        // Act
        bool updated = await BuildSubject().UpdateJobSettingsAsync(new UpdateCronJob
        {
            Function = JobNames.SyncAll,
            Description = "Every 2 hours",
            Expression = "0 0 */2 * * ?",
            Retries = 3,
            RetryIntervals = [5, 15],
            IsEnabled = false
        }, CancellationToken);

        // Assert
        AutomationCron saved = (await BuildSubject().GetJobsAsync(CancellationToken).ToListAsync(CancellationToken))
            .Single();
        using (Assert.Multiple())
        {
            await Assert.That(updated).IsTrue();
            await Assert.That(saved.Description).IsEqualTo("Every 2 hours");
            await Assert.That(saved.Expression).IsEqualTo("0 0 */2 * * ?");
            await Assert.That(saved.RetryIntervals).IsEquivalentTo([5, 15, 15], CollectionOrdering.Matching);
            await Assert.That(saved.IsEnabled).IsFalse();
        }
    }

    [Test]
    public async Task UpdateJobSettingsAsync_AnExpressionQuartzCantRead_Throws()
    {
        // Arrange
        IScheduler scheduler = await SchedulerAsync();
        await ScheduleAsync(scheduler, JobNames.SyncAll, "0 0 */4 * * ?", retries: 1, intervals: [10]);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => BuildSubject().UpdateJobSettingsAsync(
            Update(JobNames.SyncAll, "0 0 25 * * ?"), CancellationToken));
    }

    [Test]
    public async Task UpdateJobSettingsAsync_WhenTheJobHasNoSchedule_Throws()
    {
        // Arrange
        await SchedulerAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildSubject().UpdateJobSettingsAsync(
            Update(JobNames.SyncAll, "0 0 * * * ?"), CancellationToken));
    }

    [Test]
    public async Task GetLastSyncTimeAsync_WhenNothingHasSynced_ReturnsNull()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"));

        await SchedulerAsync();
        AutomationService subject = BuildSubject();

        // Act
        DateTime? lastSync = await subject.GetLastSyncTimeAsync(CancellationToken);

        // Assert
        await Assert.That(lastSync).IsNull();
    }

    [Test]
    public async Task GetLastSyncTimeAsync_ReturnsLatestSyncAcrossTheUsersAccountsInLocalTime()
    {
        // Arrange
        DateTime older = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        DateTime latest = new(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc);

        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.BankAccountScenario.WithAccount("savings", "bank"))
            .Do(s => s.BankAccountScenario.WithSyncLog("main", SyncType.Transactions, older))
            .Do(s => s.BankAccountScenario.WithSyncLog("savings", SyncType.Balance, latest));

        await SchedulerAsync();
        AutomationService subject = BuildSubject();

        // Act
        DateTime? lastSync = await subject.GetLastSyncTimeAsync(CancellationToken);

        // Assert
        await Assert.That(lastSync).IsEqualTo(latest.ToLocalTime());
    }

    [Test]
    public async Task GetLastSyncTimeAsync_IgnoresOtherUsersSyncs()
    {
        // Arrange
        DateTime mine = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        DateTime theirs = new(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc);

        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankConnectionScenario.WithConnection("theirs", OtherUsername))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.BankAccountScenario.WithAccount("their-account", "theirs"))
            .Do(s => s.BankAccountScenario.WithSyncLog("main", SyncType.Account, mine))
            .Do(s => s.BankAccountScenario.WithSyncLog("their-account", SyncType.Account, theirs));

        await SchedulerAsync();
        AutomationService subject = BuildSubject();

        // Act
        DateTime? lastSync = await subject.GetLastSyncTimeAsync(CancellationToken);

        // Assert
        await Assert.That(lastSync).IsEqualTo(mine.ToLocalTime());
    }

    [Test]
    public async Task RequestSyncAsync_SchedulesASyncForTheCurrentUser()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername));

        await SchedulerAsync();
        AutomationService subject = BuildSubject();

        // Act
        await subject.RequestSyncAsync(CancellationToken);

        // Assert
        await _syncScheduler.Received(1).ScheduleUserSyncAsync(Scenario.UserMappings[Username], CancellationToken);
    }

    [Test]
    public async Task GetJobSummariesAsync_ForAScheduledJob_GivesItsNextRunLastRunAndSuccessRate()
    {
        // Arrange: hourly, the latest failure retried and then done
        IScheduler scheduler = await SchedulerAsync();
        await ScheduleAsync(scheduler, JobNames.SyncAll, "0 0 * * * ?", retries: 1, intervals: [10]);
        await RanAsync(scheduler, JobNames.SyncAll, Now.AddHours(-3), succeeded: true);
        await RanAsync(scheduler, JobNames.SyncAll, Now.AddHours(-2), succeeded: false);
        await RanAsync(scheduler, JobNames.SyncAll, Now.AddHours(-1), succeeded: false, retryScheduled: true);
        await RanAsync(scheduler, JobNames.SyncAll, Now.AddHours(-1).AddSeconds(10), succeeded: true, retryAttempt: 1);

        // Act
        AutomationJobSummary summary = (await BuildSubject().GetJobSummariesAsync(CancellationToken)
            .ToListAsync(CancellationToken)).Single(static x => x.Function == JobNames.SyncAll);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(summary.Schedule!.Function).IsEqualTo(JobNames.SyncAll);
            await Assert.That(summary.NextRunAt).IsNotNull();
            await Assert.That(summary.LastRun!.DueAt).IsEqualTo(Now.AddHours(-1).AddSeconds(10));
            await Assert.That(summary.LastRun.RetryCount).IsEqualTo(1);
            await Assert.That(summary.SuccessRate).IsEqualTo(67);
            await Assert.That(summary.RecentRuns).IsEqualTo(3);
        }
    }

    [Test]
    public async Task GetJobSummariesAsync_ForAPausedJobThatHasNeverRun_HasNoNextOrLastRun()
    {
        // Arrange
        IScheduler scheduler = await SchedulerAsync();
        await ScheduleAsync(scheduler, JobNames.SyncAll, "0 0 * * * ?", retries: 0, intervals: []);
        await scheduler.PauseTrigger(JobSchedules.KeyFor(JobNames.SyncAll), CancellationToken);

        // Act
        AutomationJobSummary summary = (await BuildSubject().GetJobSummariesAsync(CancellationToken)
            .ToListAsync(CancellationToken)).Single(static x => x.Function == JobNames.SyncAll);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(summary.NextRunAt).IsNull();
            await Assert.That(summary.LastRun).IsNull();
            await Assert.That(summary.SuccessRate).IsNull();
        }
    }

    [Test]
    public async Task GetJobSummariesAsync_ListsTheJobsRunByHandAfterTheScheduledOnes_RunOrNot()
    {
        // Arrange: a user's sync has run, the token monitor has no schedule and has never run
        IScheduler scheduler = await SchedulerAsync();
        await ScheduleAsync(scheduler, JobNames.SyncAll, "0 0 * * * ?", retries: 0, intervals: []);
        await RanAsync(scheduler, JobNames.SyncUser, Now.AddMinutes(-5), succeeded: true, manual: true);

        // Act
        List<AutomationJobSummary> summaries = await BuildSubject().GetJobSummariesAsync(CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        AutomationJobSummary userSync = summaries.Single(x => x.Function == JobNames.SyncUser);
        AutomationJobSummary neverRun = summaries.Single(x => x.Function == JobNames.MonitorTokens);
        using (Assert.Multiple())
        {
            await Assert.That(summaries.Select(static x => x.Function))
                .IsEquivalentTo([JobNames.SyncAll, JobNames.MonitorTokens, JobNames.MoveGoalsOn, JobNames.SyncUser],
                    CollectionOrdering.Matching);
            await Assert.That(userSync.Schedule).IsNull();
            await Assert.That(userSync.LastRun!.IsManual).IsTrue();
            await Assert.That(neverRun.Schedule).IsNull();
            await Assert.That(neverRun.LastRun).IsNull();
            await Assert.That(neverRun.SuccessRate).IsNull();
        }
    }

    [Test]
    public async Task GetRunsAsync_GivesTheJobsRunsNewestFirstAPageAtATime()
    {
        // Arrange
        IScheduler scheduler = await SchedulerAsync();
        await RanAsync(scheduler, JobNames.SyncAll, Now.AddHours(-3), succeeded: true);
        await RanAsync(scheduler, JobNames.SyncAll, Now.AddHours(-1), succeeded: true, manual: true);
        await RanAsync(scheduler, JobNames.SyncAll, Now.AddHours(-2), succeeded: true);
        await RanAsync(scheduler, JobNames.MonitorTokens, Now, succeeded: true);

        // Act
        AutomationRunPage first = await BuildSubject().GetRunsAsync(JobNames.SyncAll, 0, 2, CancellationToken);
        AutomationRunPage second = await BuildSubject().GetRunsAsync(JobNames.SyncAll, 1, 2, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first.Total).IsEqualTo(3);
            await Assert.That(first.Runs.Select(static x => x.DueAt))
                .IsEquivalentTo([Now.AddHours(-1), Now.AddHours(-2)], CollectionOrdering.Matching);
            await Assert.That(first.Runs[0].IsManual).IsTrue();
            await Assert.That(second.Runs.Single().DueAt).IsEqualTo(Now.AddHours(-3));
        }
    }

    [Test]
    [Arguments(false, false, AutomationRunStatus.Failed)]
    [Arguments(false, true, AutomationRunStatus.Retrying)]
    [Arguments(true, false, AutomationRunStatus.Done)]
    public async Task GetRunsAsync_GivesARunsStatusTimingAndError(bool succeeded, bool retryScheduled,
        AutomationRunStatus expected)
    {
        // Arrange
        IScheduler scheduler = await SchedulerAsync();
        await RanAsync(scheduler, JobNames.SyncAll, Now.AddHours(-1), succeeded, retryScheduled: retryScheduled,
            error: succeeded ? null : "The bank didn't answer");

        // Act
        AutomationRun run = (await BuildSubject().GetRunsAsync(JobNames.SyncAll, 0, 10, CancellationToken)).Runs
            .Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(run.Status).IsEqualTo(expected);
            await Assert.That(run.StartedAt).IsEqualTo(Now.AddHours(-1));
            await Assert.That(run.Duration).IsEqualTo(TimeSpan.FromSeconds(5));
            await Assert.That(run.Error).IsEqualTo(succeeded ? null : "The bank didn't answer");
        }
    }

    [Test]
    public async Task GetRunsAsync_ForAJobThatHasNeverRun_IsEmpty()
    {
        // Arrange
        await SchedulerAsync();

        // Act
        AutomationRunPage page = await BuildSubject().GetRunsAsync(JobNames.SyncAll, 0, 10, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(page.Runs).IsEmpty();
            await Assert.That(page.Total).IsEqualTo(0);
        }
    }

    [Test]
    [Arguments(-1, 10)]
    [Arguments(0, 0)]
    [Arguments(0, 101)]
    public async Task GetRunsAsync_OutsideThePageLimits_Throws(int page, int pageSize)
    {
        // Arrange
        await SchedulerAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            BuildSubject().GetRunsAsync(JobNames.SyncAll, page, pageSize, CancellationToken));
    }

    [Test]
    public async Task RunNowAsync_RunsTheJobAsAManualRun()
    {
        // Arrange
        IScheduler scheduler = await SchedulerAsync();
        await ScheduleAsync(scheduler, JobNames.SyncAll, "0 0 0 1 1 ? 2099", retries: 0, intervals: []);
        await scheduler.Start(CancellationToken);
        int before = RecordingJob.Runs;

        // Act
        await BuildSubject().RunNowAsync(JobNames.SyncAll, CancellationToken);

        // Assert
        AutomationRun run = await WaitForRunAsync(JobNames.SyncAll);
        using (Assert.Multiple())
        {
            await Assert.That(run.IsManual).IsTrue();
            await Assert.That(run.Status).IsEqualTo(AutomationRunStatus.Done);
            await Assert.That(RecordingJob.Runs).IsGreaterThan(before);
        }
    }

    [Test]
    [NotInParallel(nameof(HeldJob))]
    public async Task RunNowAsync_WhileTheJobRuns_ThrowsAndShowsItRunning()
    {
        // Arrange
        HeldJob.Reset();
        IScheduler scheduler = await SchedulerAsync();
        await ScheduleAsync(scheduler, JobNames.MoveGoalsOn, "0 0 0 1 1 ? 2099", retries: 0, intervals: []);
        await scheduler.Start(CancellationToken);
        AutomationService subject = BuildSubject();
        await subject.RunNowAsync(JobNames.MoveGoalsOn, CancellationToken);
        await HeldJob.Started.WaitAsync(TestTimeouts.Wait, CancellationToken);

        try
        {
            // Act & Assert
            JobAlreadyRunningException? exception = await Assert.ThrowsAsync<JobAlreadyRunningException>(() =>
                subject.RunNowAsync(JobNames.MoveGoalsOn, CancellationToken));
            AutomationRunPage page = await subject.GetRunsAsync(JobNames.MoveGoalsOn, 0, 10, CancellationToken);

            using (Assert.Multiple())
            {
                await Assert.That(exception!.Function).IsEqualTo(JobNames.MoveGoalsOn);
                await Assert.That(page.Runs.Single().Status).IsEqualTo(AutomationRunStatus.Running);
                await Assert.That(page.Runs.Single().IsManual).IsTrue();
            }
        }
        finally
        {
            HeldJob.Release();
        }
    }

    [Test]
    public async Task RunNowAsync_ForAJobWithNoSchedule_Throws()
    {
        // Arrange
        await SchedulerAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            BuildSubject().RunNowAsync(JobNames.SyncAll, CancellationToken));
    }

    [After(Test)]
    public async Task StopQuartzAsync()
    {
        if (_quartz is null)
        {
            return;
        }

        IScheduler scheduler = await _quartz.GetRequiredService<ISchedulerFactory>().GetScheduler();
        await scheduler.Shutdown(waitForJobsToComplete: true);
        await _quartz.DisposeAsync();
    }

    /// <summary>
    /// A Quartz scheduler, not started, with the app's jobs stored durably and doing nothing but count their runs.
    /// </summary>
    private async Task<IScheduler> SchedulerAsync()
    {
        if (!Scenario.UserMappings.ContainsKey(Username))
        {
            await Scenario.Do(s => s.UserScenario.WithUser(Username));
        }

        ServiceCollection services = new();
        services.AddLogging();
        // Kept long enough for the tests' runs, months before the clock reads
        services.AddQuartzExecutionHistory(static options => options.Retention = TimeSpan.FromDays(3650));
        // A scheduler of its own for each test, as the tests run side by side
        NameValueCollection properties = new() { ["quartz.scheduler.instanceName"] = $"tests-{Guid.NewGuid()}" };
        services.AddQuartz(properties, quartz =>
        {
            foreach (string function in new[] { JobNames.SyncAll, JobNames.SyncUser, JobNames.MonitorTokens })
            {
                quartz.AddJob<RecordingJob>(job => job.WithIdentity(function).StoreDurably());
            }

            quartz.AddJob<HeldJob>(static job => job.WithIdentity(JobNames.MoveGoalsOn).StoreDurably());
        });

        _quartz = services.BuildServiceProvider();
        return await _quartz.GetRequiredService<ISchedulerFactory>().GetScheduler(CancellationToken);
    }

    private async Task ScheduleAsync(IScheduler scheduler, string function, string expression, int retries,
        int[] intervals) =>
        await scheduler.ScheduleJob(JobSchedules.Build(function, $"{function} description", expression, retries,
            intervals), default, CancellationToken);

    /// <summary>
    /// A run of <paramref name="function"/> in the history, taking 5 seconds.
    /// </summary>
    private async Task RanAsync(IScheduler scheduler, string function, DateTime firedAt, bool succeeded,
        bool retryScheduled = false, int retryAttempt = 0, bool manual = false, string? error = null)
    {
        ExecutionHistoryEntry entry = new(scheduler.SchedulerName, "test", JobKey.DefaultGroup, function,
            JobNames.ScheduleGroup, function, new DateTimeOffset(firedAt, TimeSpan.Zero), TimeSpan.FromSeconds(5),
            succeeded, error)
        {
            EntryId = Guid.NewGuid().ToString(),
            RetryScheduled = retryScheduled,
            RetryAttempt = retryAttempt,
            Manual = manual,
            Result = succeeded ? JobRunResult.Succeeded : JobRunResult.Failed
        };

        await History.AddExecution(entry, CancellationToken);
    }

    private async Task<AutomationRun> WaitForRunAsync(string function)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        timeout.CancelAfter(TestTimeouts.Wait);

        while (true)
        {
            AutomationRunPage page = await BuildSubject().GetRunsAsync(function, 0, 10, timeout.Token);
            if (page.Runs.FirstOrDefault(static x => x.Status != AutomationRunStatus.Running) is { } run)
            {
                return run;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        }
    }

    private IExecutionHistoryStore History => _quartz!.GetRequiredService<IExecutionHistoryStore>();

    private static UpdateCronJob Update(string function, string expression) => new()
    {
        Function = function, Description = "x", Expression = expression, Retries = 0, RetryIntervals = [],
        IsEnabled = true
    };

    private AutomationService BuildSubject() => new(CurrentUserFor(Username), DbContextFactory,
        NullLogger<AutomationService>.Instance, _quartz!.GetRequiredService<ISchedulerFactory>(), History,
        _syncScheduler, _time);
}
