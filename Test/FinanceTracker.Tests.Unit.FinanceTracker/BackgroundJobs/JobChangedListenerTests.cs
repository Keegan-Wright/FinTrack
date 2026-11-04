using System.Collections.Specialized;
using FinanceTracker;
using FinanceTracker.BackgroundJobs;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Quartz;

namespace FinanceTracker.Tests.Unit.FinanceTracker.BackgroundJobs;

public class JobChangedListenerTests
{
    private readonly ISyncNotifier _syncNotifier = Substitute.For<ISyncNotifier>();

    [Test]
    public async Task AJobRun_TellsAdminsAsItStartsAndFinishes()
    {
        // Arrange
        JobChangedListener subject = new(_syncNotifier);
        IJobExecutionContext context = Substitute.For<IJobExecutionContext>();
        context.JobDetail.Key.Returns(new JobKey(nameof(SyncAllOpenBankingDetailsAsync)));

        // Act
        await subject.JobToBeExecuted(context, CancellationToken.None);
        await subject.JobWasExecuted(context, new JobExecutionException("boom"), CancellationToken.None);

        // Assert
        await _syncNotifier.Received(2).JobChangedAsync(nameof(SyncAllOpenBankingDetailsAsync));
    }

    [Test]
    public async Task RegisteredAsTheAppDoes_AFailingJob_StillTellsAdminsItStartedAndFinished()
    {
        // Arrange: registered with no matchers, as AddBackgroundJobs does, for every job
        TaskCompletionSource finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int changes = 0;
        _syncNotifier.JobChangedAsync("failing").Returns(_ =>
        {
            if (Interlocked.Increment(ref changes) == 2)
            {
                finished.TrySetResult();
            }

            return Task.CompletedTask;
        });

        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(_syncNotifier);
        NameValueCollection properties = new() { ["quartz.scheduler.instanceName"] = $"listener-{Guid.NewGuid()}" };
        services.AddQuartz(properties, static quartz =>
        {
            quartz.AddJob<FailingJob>(static job => job.WithIdentity("failing").StoreDurably());
            quartz.AddJobListener<JobChangedListener>([]);
        });
        await using ServiceProvider provider = services.BuildServiceProvider();
        IScheduler scheduler = await provider.GetRequiredService<ISchedulerFactory>().GetScheduler();
        await scheduler.Start();

        // Act
        await scheduler.TriggerJob(new JobKey("failing"), null, CancellationToken.None);

        // Assert
        await finished.Task.WaitAsync(TestTimeouts.Wait);
        await scheduler.Shutdown(waitForJobsToComplete: true);
        await Assert.That(changes).IsEqualTo(2);
    }
}
