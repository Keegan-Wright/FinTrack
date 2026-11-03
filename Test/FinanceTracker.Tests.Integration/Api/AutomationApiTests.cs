using System.Net;
using FinanceTracker.Contracts.Automation;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using FinanceTracker.Tests.Unit.Shared;
using TickerQ.Utilities.Enums;

namespace FinanceTracker.Tests.Integration.Api;

public class AutomationApiTests : IntegrationTestBase
{
    private const string MoveGoalsFunction = "MoveEventSavingsGoalsOnAsync";

    [Test]
    public async Task UpdateJobSettings_AsAnAdmin_IsSaved()
    {
        // Arrange
        SignedInUser admin = await RegisterAdminAsync();
        IAutomationService service = ApiFor(admin).Get<IAutomationService>();
        AutomationCron job = (await service.GetJobsAsync(CancellationToken).ToListAsync(CancellationToken)).First();
        string description = UniqueName("description");

        // Act
        await service.UpdateJobSettingsAsync(new UpdateCronJob
        {
            Id = job.Id,
            Description = description,
            Expression = job.Expression,
            Retries = job.Retries,
            RetryIntervals = job.RetryIntervals ?? [],
            IsEnabled = job.IsEnabled
        }, CancellationToken);

        // Assert
        List<AutomationCron> jobs = await service.GetJobsAsync(CancellationToken).ToListAsync(CancellationToken);
        await Assert.That(jobs.Single(x => x.Id == job.Id).Description).IsEqualTo(description);
    }

    [Test]
    public async Task UpdateJobSettings_ForAMissingJob_ThrowsAsTheServiceDoes()
    {
        // Arrange
        SignedInUser admin = await RegisterAdminAsync();
        IAutomationService service = ApiFor(admin).Get<IAutomationService>();
        AutomationCron job = (await service.GetJobsAsync(CancellationToken).ToListAsync(CancellationToken)).First();

        // Act & Assert, a real job's valid settings under an id no job has
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateJobSettingsAsync(new UpdateCronJob
        {
            Id = Guid.NewGuid(),
            Description = job.Description,
            Expression = job.Expression,
            Retries = job.Retries,
            RetryIntervals = job.RetryIntervals ?? [],
            IsEnabled = job.IsEnabled
        }, CancellationToken));
    }

    [Test]
    public async Task GetJobSummaries_AsAnAdmin_ListsEveryScheduledJob()
    {
        // Arrange
        SignedInUser admin = await RegisterAdminAsync();
        IAutomationService service = ApiFor(admin).Get<IAutomationService>();

        // Act
        List<AutomationJobSummary> summaries = await service.GetJobSummariesAsync(CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert, the app's own jobs, other tests add theirs alongside
        List<string> scheduled = summaries.Where(x => x.Schedule is not null).Select(x => x.Function).ToList();
        await Assert.That(scheduled).Contains("SyncAllOpenBankingDetailsAsync")
            .And.Contains("MonitorOpenBankingTokenExpirationAsync")
            .And.Contains(MoveGoalsFunction);
    }

    [Test]
    public async Task GetRuns_AsAnAdmin_GivesAPageOfTheJobsRuns()
    {
        // Arrange
        SignedInUser admin = await RegisterAdminAsync();
        IAutomationService service = ApiFor(admin).Get<IAutomationService>();
        string function = UniqueName("job");
        await Scenario.Do(s => s.AutomationScenario.WithTimeTicker(function, TickerStatus.Done, DateTime.UtcNow));

        // Act
        AutomationRunPage page = await service.GetRunsAsync(function, page: 0, pageSize: 25, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(page.Total).IsEqualTo(1);
            await Assert.That(page.Runs.Single().IsManual).IsTrue();
            await Assert.That(page.Runs.Single().Status).IsEqualTo(AutomationRunStatus.Done);
        }
    }

    [Test]
    public async Task GetRuns_WithTooBigAPage_IsABadRequest()
    {
        // Arrange
        SignedInUser admin = await RegisterAdminAsync();
        IAutomationService service = ApiFor(admin).Get<IAutomationService>();

        // Act
        HttpRequestException? exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.GetRunsAsync("SyncAllOpenBankingDetailsAsync", page: 0, IAutomationService.MaxRunsPageSize + 1,
                CancellationToken));

        // Assert
        await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task RunNow_AsAnAdmin_RunsTheJobAndTellsTheirBrowserAsItStartsAndFinishes()
    {
        // Arrange
        SignedInUser admin = await RegisterAdminAsync();
        ApiSession api = ApiFor(admin);
        IAutomationService service = api.Get<IAutomationService>();
        AutomationCron job = (await service.GetJobsAsync(CancellationToken).ToListAsync(CancellationToken))
            .Single(x => x.Function == MoveGoalsFunction);

        int changes = 0;
        TaskCompletionSource finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using IDisposable subscription = await api.Get<ISyncNotifications>().SubscribeToJobsAsync(function =>
        {
            if (function == MoveGoalsFunction && Interlocked.Increment(ref changes) == 2)
            {
                finished.TrySetResult();
            }

            return Task.CompletedTask;
        }, CancellationToken);

        // Act
        await service.RunNowAsync(job.Id, CancellationToken);

        // Assert, told as it starts and as it finishes, then TickerQ records the run as done
        await finished.Task.WaitAsync(TestTimeouts.Job, CancellationToken);
        AutomationRun run = await WaitForManualRunAsync(service, MoveGoalsFunction);
        await Assert.That(run.Status).IsEqualTo(AutomationRunStatus.Done);
    }

    [Test]
    public async Task RunNow_WhileTheJobRuns_IsAConflict()
    {
        // Arrange, a paused job of its own with a run going
        SignedInUser admin = await RegisterAdminAsync();
        IAutomationService service = ApiFor(admin).Get<IAutomationService>();
        string function = UniqueName("Job");
        await Scenario.Do(s => s.AutomationScenario.WithCronJob(function, function, cron => cron.IsEnabled = false))
            .Do(s => s.AutomationScenario.WithTimeTicker(function, TickerStatus.InProgress, DateTime.UtcNow));

        // Act & Assert
        JobAlreadyRunningException? exception = await Assert.ThrowsAsync<JobAlreadyRunningException>(() =>
            service.RunNowAsync(Scenario.CronTickerMappings[function], CancellationToken));
        await Assert.That(exception!.Function).IsEqualTo(function);
    }

    [Test]
    public async Task RunNow_ForAMissingJob_ThrowsAsTheServiceDoes()
    {
        // Arrange
        SignedInUser admin = await RegisterAdminAsync();
        IAutomationService service = ApiFor(admin).Get<IAutomationService>();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunNowAsync(Guid.NewGuid(), CancellationToken));
    }

    [Test]
    public async Task GetLastSyncTime_IsNullUntilTheUsersAccountsSync()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IAutomationService service = ApiFor(user).Get<IAutomationService>();
        DateTime syncedAt = new(2026, 3, 1, 8, 30, 0, DateTimeKind.Utc);

        // Act
        DateTime? beforeSync = await service.GetLastSyncTimeAsync(CancellationToken);
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("bank", user.Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.BankAccountScenario.WithSyncLog("main", SyncType.Balance, syncedAt));
        DateTime? afterSync = await service.GetLastSyncTimeAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(beforeSync).IsNull();
            await Assert.That(afterSync?.ToUniversalTime()).IsEqualTo(syncedAt);
        }
    }

    [Test]
    public async Task RequestSync_NotifiesTheUsersBrowserThroughTheSyncHub()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        ApiSession api = ApiFor(user);
        TaskCompletionSource synced = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using IDisposable subscription = await api.Get<ISyncNotifications>().SubscribeAsync(() =>
        {
            synced.TrySetResult();
            return Task.CompletedTask;
        }, CancellationToken);

        // Act
        await api.Get<IAutomationService>().RequestSyncAsync(CancellationToken);

        // Assert, the user sync always notifies, even with nothing to sync
        await synced.Task.WaitAsync(TestTimeouts.Job, CancellationToken);
        await Assert.That(synced.Task.IsCompletedSuccessfully).IsTrue();
    }

    // TickerQ saves how a run went just after the job says it's finished
    private async Task<AutomationRun> WaitForManualRunAsync(IAutomationService service, string function)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        timeout.CancelAfter(TestTimeouts.Job);

        while (true)
        {
            AutomationRunPage page = await service.GetRunsAsync(function, page: 0, pageSize: 25, timeout.Token);
            AutomationRun? run = page.Runs.FirstOrDefault(x => x is { IsManual: true, Status: AutomationRunStatus.Done });
            if (run is not null)
            {
                return run;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), timeout.Token);
        }
    }
}
