using FinanceTracker.Contracts.Automation;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Infrastructure.Automation;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Interfaces.Managers;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Automation;

public class AutomationServiceTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";
    private const string SyncFunction = "SyncAllOpenBankingDetailsAsync";

    private readonly ICronTickerManager<CronTickerEntity> _cronTickerManager =
        Substitute.For<ICronTickerManager<CronTickerEntity>>();

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

    private AutomationService BuildSubject() => new(CurrentUserFor(Username), DbContextFactory,
        NullLogger<AutomationService>.Instance, _cronTickerManager, Substitute.For<ITimeTickerManager<TimeTickerEntity>>());
}
