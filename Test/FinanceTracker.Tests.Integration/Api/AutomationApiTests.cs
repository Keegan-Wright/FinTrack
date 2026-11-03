using System.Net;
using FinanceTracker.Contracts.Automation;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using TickerQ.Utilities.Enums;

namespace FinanceTracker.Tests.Integration.Api;

public class AutomationApiTests : IntegrationTestBase
{
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
        List<AutomationCron> jobs = await service.GetJobsAsync(CancellationToken).ToListAsync(CancellationToken);

        // Act
        List<AutomationJobSummary> summaries = await service.GetJobSummariesAsync(CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        await Assert.That(summaries.Where(x => x.Schedule is not null).Select(x => x.Function))
            .IsEquivalentTo(jobs.Select(x => x.Function));
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
        await synced.Task.WaitAsync(TimeSpan.FromSeconds(90), CancellationToken);
        await Assert.That(synced.Task.IsCompletedSuccessfully).IsTrue();
    }
}
