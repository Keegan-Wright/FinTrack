using FinanceTracker.Contracts.Automation;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared.Scenarios;

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
