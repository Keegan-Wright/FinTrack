using FinanceTracker.Contracts.Automation;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Automation;
using FinanceTracker.Infrastructure.OpenBanking;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Enums;
using TUnit.Assertions.Enums;
using TickerQ.Utilities.Interfaces.Managers;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Automation;

public class AutomationServiceTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";
    private const string SyncFunction = "SyncAllOpenBankingDetailsAsync";
    private const string UserSyncFunction = "SyncUserOpenBankingDetailsAsync";

    private readonly ICronTickerManager<CronTickerEntity> _cronTickerManager =
        Substitute.For<ICronTickerManager<CronTickerEntity>>();

    private readonly IOpenBankingSyncScheduler _syncScheduler = Substitute.For<IOpenBankingSyncScheduler>();

    private readonly ITimeTickerManager<TimeTickerEntity> _timeTickerManager =
        Substitute.For<ITimeTickerManager<TimeTickerEntity>>();

    private static readonly DateTime Now = new(2026, 3, 10, 12, 0, 30, DateTimeKind.Utc);
    private readonly FixedTimeProvider _time = new(Now);

    [Test]
    public async Task GetJobsAsync_ReturnsPersistedCronDefinitions()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.AutomationScenario.WithCronJob("sync", SyncFunction, cron =>
            {
                cron.Description = "sync";
                cron.Expression = "*/5 * * * *";
                cron.Retries = 3;
                cron.RetryIntervals = [10, 20, 30];
                cron.IsEnabled = false;
            }))
            .Do(s => s.AutomationScenario.WithCronJob("monitor", "MonitorOpenBankingTokenExpirationAsync"));

        AutomationService subject = BuildSubject();

        // Act
        List<AutomationCron> jobs = await subject.GetJobsAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert
        AutomationCron sync = jobs.Single(x => x.Function == SyncFunction);

        using (Assert.Multiple())
        {
            await Assert.That(jobs.Count).IsEqualTo(2);
            await Assert.That(sync.Id).IsEqualTo(Scenario.CronTickerMappings["sync"]);
            await Assert.That(sync.Description).IsEqualTo("sync");
            await Assert.That(sync.Expression).IsEqualTo("*/5 * * * *");
            await Assert.That(sync.Retries).IsEqualTo(3);
            await Assert.That(sync.RetryIntervals).IsEquivalentTo([10, 20, 30]);
            await Assert.That(sync.IsEnabled).IsFalse();
        }
    }

    [Test]
    public async Task GetJobsAsync_WhenNoJobsExist_ReturnsEmpty()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        AutomationService subject = BuildSubject();

        // Act
        List<AutomationCron> jobs = await subject.GetJobsAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert
        await Assert.That(jobs.Count).IsEqualTo(0);
    }

    [Test]
    public async Task UpdateJobSettingsAsync_PassesUpdatedJobToCronManager()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.AutomationScenario.WithCronJob("sync", SyncFunction));

        AutomationService subject = BuildSubject();
        Guid cronId = Scenario.CronTickerMappings["sync"];

        // Act
        bool result = await subject.UpdateJobSettingsAsync(new UpdateCronJob
        {
            Id = cronId,
            Description = "Updated",
            Expression = "0 0 * * *",
            Retries = 5,
            RetryIntervals = [1, 2],
            IsEnabled = false
        }, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result).IsTrue();
            await _cronTickerManager.Received(1).UpdateAsync(Arg.Is<CronTickerEntity>(x =>
                x.Id == cronId &&
                x.Description == "Updated" &&
                x.Expression == "0 0 * * *" &&
                x.Retries == 5 &&
                x.RetryIntervals!.SequenceEqual(new[] { 1, 2 }) &&
                !x.IsEnabled), CancellationToken);
        }
    }

    [Test]
    public async Task UpdateJobSettingsAsync_WhenJobDoesNotExist_Throws()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        AutomationService subject = BuildSubject();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => subject.UpdateJobSettingsAsync(new UpdateCronJob
        {
            Id = Guid.NewGuid(),
            Description = "Updated",
            Expression = "0 0 * * *",
            RetryIntervals = [],
            IsEnabled = true
        }, CancellationToken));
    }

    [Test]
    public async Task GetLastSyncTimeAsync_WhenNothingHasSynced_ReturnsNull()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"));

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

        AutomationService subject = BuildSubject();

        // Act
        await subject.RequestSyncAsync(CancellationToken);

        // Assert
        await _syncScheduler.Received(1).ScheduleUserSyncAsync(Scenario.UserMappings[Username], CancellationToken);
    }

    [Test]
    public async Task GetJobSummariesAsync_ForAScheduledJob_GivesItsNextRunLastRunAndSuccessRate()
    {
        // Arrange, three finished runs (one failed), the latest still going, and the next one queued up
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.AutomationScenario.WithCronJob("sync", SyncFunction, cron => cron.Expression = "0 0 * * * *"))
            .Do(s => s.AutomationScenario.WithCronOccurrence("sync", TickerStatus.Done, Now.AddHours(-3)))
            .Do(s => s.AutomationScenario.WithCronOccurrence("sync", TickerStatus.Failed, Now.AddHours(-2)))
            .Do(s => s.AutomationScenario.WithCronOccurrence("sync", TickerStatus.DueDone, Now.AddHours(-1)))
            .Do(s => s.AutomationScenario.WithCronOccurrence("sync", TickerStatus.InProgress, Now.AddMinutes(-1)))
            .Do(s => s.AutomationScenario.WithCronOccurrence("sync", TickerStatus.Idle, Now.AddMinutes(59)));

        AutomationService subject = BuildSubject();

        // Act
        List<AutomationJobSummary> summaries = await subject.GetJobSummariesAsync(CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        AutomationJobSummary sync = summaries.Single();

        using (Assert.Multiple())
        {
            await Assert.That(sync.Function).IsEqualTo(SyncFunction);
            await Assert.That(sync.Schedule?.Id).IsEqualTo(Scenario.CronTickerMappings["sync"]);
            await Assert.That(sync.NextRunAt).IsEqualTo(_time.ToLocal(new DateTime(2026, 3, 10, 13, 0, 0)));
            await Assert.That(sync.LastRun?.Status).IsEqualTo(AutomationRunStatus.Running);
            await Assert.That(sync.LastRun?.DueAt).IsEqualTo(_time.ToLocal(Now.AddMinutes(-1)));
            await Assert.That(sync.SuccessRate).IsEqualTo(67);
            await Assert.That(sync.RecentRuns).IsEqualTo(3);
        }
    }

    [Test]
    public async Task GetJobSummariesAsync_ForAPausedJobThatHasNeverRun_HasNoNextOrLastRun()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.AutomationScenario.WithCronJob("sync", SyncFunction, cron => cron.IsEnabled = false));

        AutomationService subject = BuildSubject();

        // Act
        List<AutomationJobSummary> summaries = await subject.GetJobSummariesAsync(CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        AutomationJobSummary sync = summaries.Single();

        using (Assert.Multiple())
        {
            await Assert.That(sync.NextRunAt).IsNull();
            await Assert.That(sync.LastRun).IsNull();
            await Assert.That(sync.SuccessRate).IsNull();
            await Assert.That(sync.RecentRuns).IsEqualTo(0);
        }
    }

    [Test]
    public async Task GetJobSummariesAsync_ListsJobsOnlyRunByHandAfterTheScheduledOnes()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.AutomationScenario.WithCronJob("sync", SyncFunction))
            .Do(s => s.AutomationScenario.WithTimeTicker(SyncFunction, TickerStatus.Done, Now.AddHours(-1)))
            .Do(s => s.AutomationScenario.WithTimeTicker(UserSyncFunction, TickerStatus.Done, Now.AddHours(-2)))
            .Do(s => s.AutomationScenario.WithTimeTicker(UserSyncFunction, TickerStatus.Failed, Now.AddHours(-1)));

        AutomationService subject = BuildSubject();

        // Act
        List<AutomationJobSummary> summaries = await subject.GetJobSummariesAsync(CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(summaries.Select(x => x.Function)).IsEquivalentTo([SyncFunction, UserSyncFunction],
                CollectionOrdering.Matching);
            await Assert.That(summaries[0].LastRun?.IsManual).IsTrue();
            await Assert.That(summaries[1].Schedule).IsNull();
            await Assert.That(summaries[1].NextRunAt).IsNull();
            await Assert.That(summaries[1].LastRun?.Status).IsEqualTo(AutomationRunStatus.Failed);
            await Assert.That(summaries[1].SuccessRate).IsEqualTo(50);
        }
    }

    [Test]
    public async Task GetRunsAsync_MergesScheduledAndManualRunsNewestFirstAPageAtATime()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.AutomationScenario.WithCronJob("sync", SyncFunction))
            .Do(s => s.AutomationScenario.WithCronOccurrence("sync", TickerStatus.Done, Now.AddHours(-4)))
            .Do(s => s.AutomationScenario.WithTimeTicker(SyncFunction, TickerStatus.Done, Now.AddHours(-3)))
            .Do(s => s.AutomationScenario.WithCronOccurrence("sync", TickerStatus.Done, Now.AddHours(-2)))
            .Do(s => s.AutomationScenario.WithTimeTicker(SyncFunction, TickerStatus.Done, Now.AddHours(-1)))
            .Do(s => s.AutomationScenario.WithTimeTicker(UserSyncFunction, TickerStatus.Done, Now));

        AutomationService subject = BuildSubject();

        // Act
        AutomationRunPage first = await subject.GetRunsAsync(SyncFunction, page: 0, pageSize: 3, CancellationToken);
        AutomationRunPage second = await subject.GetRunsAsync(SyncFunction, page: 1, pageSize: 3, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first.Total).IsEqualTo(4);
            await Assert.That(first.Runs.Select(x => x.IsManual)).IsEquivalentTo([true, false, true],
                CollectionOrdering.Matching);
            await Assert.That(first.Runs.Select(x => x.DueAt)).IsEquivalentTo(
                [_time.ToLocal(Now.AddHours(-1)), _time.ToLocal(Now.AddHours(-2)), _time.ToLocal(Now.AddHours(-3))],
                CollectionOrdering.Matching);
            await Assert.That(second.Runs.Select(x => x.DueAt)).IsEquivalentTo([_time.ToLocal(Now.AddHours(-4))]);
        }
    }

    [Test]
    public async Task GetRunsAsync_GivesARunsTimingRetriesAndError_StartingWhenItFinishedLessHowLongItTook()
    {
        // Arrange
        DateTime startedAt = Now.AddMinutes(-10).AddSeconds(2);
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.AutomationScenario.WithCronJob("sync", SyncFunction))
            .Do(s => s.AutomationScenario.WithCronOccurrence("sync", TickerStatus.Failed, Now.AddMinutes(-10),
                occurrence =>
                {
                    occurrence.LockedAt = startedAt.AddMinutes(-30);
                    occurrence.ExecutedAt = startedAt.AddMilliseconds(4500);
                    occurrence.ElapsedTime = 4500;
                    occurrence.RetryCount = 2;
                    occurrence.ExceptionMessage = "The bank timed out";
                }));

        AutomationService subject = BuildSubject();

        // Act
        AutomationRunPage page = await subject.GetRunsAsync(SyncFunction, page: 0, pageSize: 25, CancellationToken);

        // Assert
        AutomationRun run = page.Runs.Single();

        using (Assert.Multiple())
        {
            await Assert.That(run.Function).IsEqualTo(SyncFunction);
            await Assert.That(run.Status).IsEqualTo(AutomationRunStatus.Failed);
            await Assert.That(run.StartedAt).IsEqualTo(_time.ToLocal(startedAt));
            await Assert.That(run.Duration).IsEqualTo(TimeSpan.FromMilliseconds(4500));
            await Assert.That(run.RetryCount).IsEqualTo(2);
            await Assert.That(run.Error).IsEqualTo("The bank timed out");
        }
    }

    [Test]
    public async Task GetRunsAsync_ForTheNextRunQueuedEarly_HasNotStarted()
    {
        // Arrange, TickerQ queues and locks the next run well before it's due
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.AutomationScenario.WithCronJob("sync", SyncFunction))
            .Do(s => s.AutomationScenario.WithCronOccurrence("sync", TickerStatus.Queued, Now.AddHours(1),
                occurrence => occurrence.LockedAt = Now.AddMinutes(-5)));

        AutomationService subject = BuildSubject();

        // Act
        AutomationRunPage page = await subject.GetRunsAsync(SyncFunction, page: 0, pageSize: 25, CancellationToken);

        // Assert
        AutomationRun run = page.Runs.Single();

        using (Assert.Multiple())
        {
            await Assert.That(run.Status).IsEqualTo(AutomationRunStatus.Waiting);
            await Assert.That(run.StartedAt).IsNull();
            await Assert.That(run.DueAt).IsEqualTo(_time.ToLocal(Now.AddHours(1)));
        }
    }

    [Test]
    public async Task GetRunsAsync_ForARunLockedBeforeItWasDue_StartedWhenDue()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.AutomationScenario.WithCronJob("sync", SyncFunction))
            .Do(s => s.AutomationScenario.WithCronOccurrence("sync", TickerStatus.InProgress, Now.AddMinutes(-1),
                occurrence => occurrence.LockedAt = Now.AddMinutes(-40)));

        AutomationService subject = BuildSubject();

        // Act
        AutomationRunPage page = await subject.GetRunsAsync(SyncFunction, page: 0, pageSize: 25, CancellationToken);

        // Assert
        await Assert.That(page.Runs.Single().StartedAt).IsEqualTo(_time.ToLocal(Now.AddMinutes(-1)));
    }

    [Test]
    public async Task GetRunsAsync_ForAJobThatHasNeverRun_IsEmpty()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        AutomationService subject = BuildSubject();

        // Act
        AutomationRunPage page = await subject.GetRunsAsync(SyncFunction, page: 0, pageSize: 25, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(page.Runs).IsEmpty();
            await Assert.That(page.Total).IsEqualTo(0);
        }
    }

    [Test]
    [Arguments(-1, 25)]
    [Arguments(0, 0)]
    [Arguments(0, 101)]
    public async Task GetRunsAsync_OutsideThePageLimits_Throws(int page, int pageSize)
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        AutomationService subject = BuildSubject();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            subject.GetRunsAsync(SyncFunction, page, pageSize, CancellationToken));
    }

    [Test]
    public async Task RunNowAsync_QueuesARunOfTheJobNowRetriedLikeItsSchedule()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.AutomationScenario.WithCronJob("sync", SyncFunction, cron =>
            {
                cron.Retries = 3;
                cron.RetryIntervals = [10, 20, 30];
            }))
            .Do(s => s.AutomationScenario.WithCronOccurrence("sync", TickerStatus.Done, Now.AddHours(-1)))
            .Do(s => s.AutomationScenario.WithCronOccurrence("sync", TickerStatus.Queued, Now.AddHours(1)))
            .Do(s => s.AutomationScenario.WithTimeTicker(SyncFunction, TickerStatus.Failed, Now.AddHours(-2)));

        AutomationService subject = BuildSubject();

        // Act, the next scheduled run queued ahead of time doesn't stop it
        await subject.RunNowAsync(Scenario.CronTickerMappings["sync"], CancellationToken);

        // Assert
        await _timeTickerManager.Received(1).AddAsync(Arg.Is<TimeTickerEntity>(x =>
            x.Function == SyncFunction &&
            x.ExecutionTime == Now &&
            x.Retries == 3 &&
            x.RetryIntervals!.SequenceEqual(new[] { 10, 20, 30 })), CancellationToken);
    }

    [Test]
    [Arguments(TickerStatus.InProgress, false)]
    [Arguments(TickerStatus.Queued, false)]
    [Arguments(TickerStatus.InProgress, true)]
    [Arguments(TickerStatus.Queued, true)]
    [Arguments(TickerStatus.Idle, true)]
    public async Task RunNowAsync_WhileARunIsGoing_ThrowsAndQueuesNothing(TickerStatus status, bool manual)
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.AutomationScenario.WithCronJob("sync", SyncFunction));
        if (manual)
        {
            await Scenario.Do(s => s.AutomationScenario.WithTimeTicker(SyncFunction, status, Now));
        }
        else
        {
            await Scenario.Do(s => s.AutomationScenario.WithCronOccurrence("sync", status, Now));
        }

        AutomationService subject = BuildSubject();

        // Act
        JobAlreadyRunningException? exception = await Assert.ThrowsAsync<JobAlreadyRunningException>(() =>
            subject.RunNowAsync(Scenario.CronTickerMappings["sync"], CancellationToken));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exception!.Function).IsEqualTo(SyncFunction);
            await _timeTickerManager.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        }
    }

    [Test]
    public async Task RunNowAsync_ForAMissingJob_Throws()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        AutomationService subject = BuildSubject();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => subject.RunNowAsync(Guid.NewGuid(), CancellationToken));
    }

    private AutomationService BuildSubject() => new(CurrentUserFor(Username), DbContextFactory,
        NullLogger<AutomationService>.Instance, _cronTickerManager, _timeTickerManager, _syncScheduler, _time);
}
