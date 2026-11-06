using System.Net;
using FinanceTracker.Contracts.Automation;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Tests.Integration.Http;
using Microsoft.EntityFrameworkCore;
using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using FinanceTracker.Tests.Unit.Shared;

namespace FinanceTracker.Tests.Integration.Api;

public class AutomationApiTests : IntegrationTestBase
{
    internal const string MoveGoalsFunction = "MoveEventSavingsGoalsOnAsync";

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
            Function = job.Function,
            Description = description,
            Expression = job.Expression,
            Retries = job.Retries,
            RetryIntervals = job.RetryIntervals,
            IsEnabled = job.IsEnabled
        }, CancellationToken);

        // Assert
        List<AutomationCron> jobs = await service.GetJobsAsync(CancellationToken).ToListAsync(CancellationToken);
        await Assert.That(jobs.Single(x => x.Function == job.Function).Description).IsEqualTo(description);
    }

    [Test]
    public async Task UpdateJobSettings_ForAMissingJob_ThrowsAsTheServiceDoes()
    {
        // Arrange
        SignedInUser admin = await RegisterAdminAsync();
        IAutomationService service = ApiFor(admin).Get<IAutomationService>();
        AutomationCron job = (await service.GetJobsAsync(CancellationToken).ToListAsync(CancellationToken)).First();

        // Act & Assert, a real job's valid settings under a name no job has
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateJobSettingsAsync(new UpdateCronJob
        {
            Function = UniqueName("job"),
            Description = job.Description,
            Expression = job.Expression,
            Retries = job.Retries,
            RetryIntervals = job.RetryIntervals,
            IsEnabled = job.IsEnabled
        }, CancellationToken));
    }

    [Test]
    public async Task GetJobSummaries_AsAnAdmin_ListsEveryJob()
    {
        // Arrange
        SignedInUser admin = await RegisterAdminAsync();
        IAutomationService service = ApiFor(admin).Get<IAutomationService>();

        // Act
        List<AutomationJobSummary> summaries = await service.GetJobSummariesAsync(CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert, the app's own jobs, other tests add theirs alongside
        List<string> scheduled = summaries.Where(x => x.Schedule is not null).Select(x => x.Function).ToList();
        using (Assert.Multiple())
        {
            await Assert.That(scheduled).Contains("SyncAllOpenBankingDetailsAsync")
                .And.Contains("MonitorOpenBankingTokenExpirationAsync")
                .And.Contains(MoveGoalsFunction);

            // A user's sync now has no schedule, it's listed whether or not anyone has asked for one yet
            await Assert.That(summaries.Single(x => x.Function == "SyncUserOpenBankingDetailsAsync").Schedule)
                .IsNull();
        }
    }

    [Test]
    public async Task GetJobs_EachJobStartsOnItsScheduleInQuartzsCron_AndTickerQsTablesAreGone()
    {
        // Arrange
        SignedInUser admin = await RegisterAdminAsync();
        IAutomationService service = ApiFor(admin).Get<IAutomationService>();

        // Act
        List<AutomationCron> jobs = await service.GetJobsAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        bool tickerQLeft = await context.Database.SqlQueryRaw<bool>(
                "SELECT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'ticker') AS \"Value\"")
            .SingleAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(jobs.Single(x => x.Function == "SyncAllOpenBankingDetailsAsync").Expression)
                .IsEqualTo("0 0 */4 * * ?");
            await Assert.That(jobs.Single(x => x.Function == MoveGoalsFunction).RetryIntervals)
                .IsEquivalentTo([10], TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(tickerQLeft).IsFalse();
        }
    }

    [Test]
    public async Task GetRuns_ForAJobThatHasntRun_IsAnEmptyPage()
    {
        // Arrange
        SignedInUser admin = await RegisterAdminAsync();
        IAutomationService service = ApiFor(admin).Get<IAutomationService>();

        // Act
        AutomationRunPage page = await service.GetRunsAsync(UniqueName("job"), page: 0, pageSize: 25,
            CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(page.Total).IsEqualTo(0);
            await Assert.That(page.Runs).IsEmpty();
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
    [NotInParallel(MoveGoalsFunction)]
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
        await service.RunNowAsync(job.Function, CancellationToken);

        // Assert, told as it starts and as it finishes, with the run saved as done
        await finished.Task.WaitAsync(TestTimeouts.Job, CancellationToken);
        AutomationRun run = await WaitForManualRunAsync(service, MoveGoalsFunction);
        await Assert.That(run.Status).IsEqualTo(AutomationRunStatus.Done);
    }

    [Test]
    public async Task RunNow_ForAMissingJob_ThrowsAsTheServiceDoes()
    {
        // Arrange
        SignedInUser admin = await RegisterAdminAsync();
        IAutomationService service = ApiFor(admin).Get<IAutomationService>();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunNowAsync(UniqueName("job"), CancellationToken));
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

    [Test]
    // A sync of every user's banks would reach this user's too, so not while those tests run
    [NotInParallel(nameof(OpenBanking.BackgroundSyncTests))]
    public async Task RequestSync_ForAUserWithABank_TellsTheirBrowserItStartedThenFinished()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("bank", user.Username));
        ApiSession api = ApiFor(user);
        ISyncNotifications notifications = api.Get<ISyncNotifications>();
        List<string> heard = [];
        TaskCompletionSource synced = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using IDisposable started = await notifications.SubscribeToSyncStartsAsync(() =>
        {
            lock (heard)
            {
                heard.Add("started");
            }

            return Task.CompletedTask;
        }, CancellationToken);
        using IDisposable finished = await notifications.SubscribeAsync(() =>
        {
            lock (heard)
            {
                heard.Add("finished");
            }

            synced.TrySetResult();
            return Task.CompletedTask;
        }, CancellationToken);

        // Act
        await api.Get<IAutomationService>().RequestSyncAsync(CancellationToken);

        // Assert
        await synced.Task.WaitAsync(TestTimeouts.Job, CancellationToken);
        await Assert.That(heard).IsEquivalentTo(["started", "finished"],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    // The run is looked for by its status, any earlier run of the job may be on the page too
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
