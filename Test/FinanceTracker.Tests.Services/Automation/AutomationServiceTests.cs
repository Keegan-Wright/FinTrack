using FinanceTracker.Data;
using FinanceTracker.Services.Automation;
using FinanceTracker.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Enums;
using TickerQ.Utilities.Interfaces.Managers;

namespace FinanceTracker.Tests.Services.Automation;

public class AutomationServiceTests : ServiceTestsFixtureBase
{
    [Test]
    public async Task GetJobsAsync_ReturnsPersistedCronDefinitions()
    {
        IDbContextFactory<FinanceTrackerContext> factory = BuildFactory($"automation-{Guid.NewGuid()}");
        await SeedUsersAsync(factory, _cancellationTokenSource.Token);

        await using (FinanceTrackerContext context = await factory.CreateDbContextAsync(_cancellationTokenSource.Token))
        {
            context.CronTickerEntities.Add(new CronTickerEntity
            {
                Id = Guid.NewGuid(),
                Function = "SyncAllOpenBankingDetailsAsync",
                Description = "sync",
                Expression = "* * * * *",
                Retries = 1,
                RetryIntervals = [1],
                IsEnabled = true
            });

            await context.SaveChangesAsync(_cancellationTokenSource.Token);
        }

        var cronManager = Substitute.For<ICronTickerManager<CronTickerEntity>>();
        var timeManager = Substitute.For<ITimeTickerManager<TimeTickerEntity>>();

        var subject = new AutomationService(BuildUser(PrimaryUserId), factory, NullLogger<AutomationService>.Instance,
            cronManager, timeManager);

        List<string> functions = [];
        await foreach (var job in subject.GetJobsAsync(_cancellationTokenSource.Token))
        {
            functions.Add(job.Function);
        }

        await Assert.That(functions).Contains("SyncAllOpenBankingDetailsAsync");
    }

    [Test]
    public async Task GetJobsAsync_WhenNoJobsExist_ReturnsEmpty()
    {
        IDbContextFactory<FinanceTrackerContext> factory = BuildFactory($"automation-empty-{Guid.NewGuid()}");
        await SeedUsersAsync(factory, _cancellationTokenSource.Token);

        var cronManager = Substitute.For<ICronTickerManager<CronTickerEntity>>();
        var timeManager = Substitute.For<ITimeTickerManager<TimeTickerEntity>>();

        var subject = new AutomationService(BuildUser(PrimaryUserId), factory, NullLogger<AutomationService>.Instance,
            cronManager, timeManager);

        var functions = new List<string>();
        await foreach (var job in subject.GetJobsAsync(_cancellationTokenSource.Token))
        {
            functions.Add(job.Function);
        }

        await Assert.That(functions.Count).IsEqualTo(0);
    }
}
