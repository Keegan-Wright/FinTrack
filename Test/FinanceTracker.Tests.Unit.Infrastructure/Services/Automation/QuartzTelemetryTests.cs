using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Automation;

/// <summary>
/// Quartz publishes a span and metrics for each job run under the name the app's OpenTelemetry set-up listens for,
/// <c>Quartz</c>, see the service defaults.
/// </summary>
[NotInParallel(nameof(QuartzTelemetryTests))]
public class QuartzTelemetryTests : UnitTestBase
{
    private const string QuartzInstrumentationName = "Quartz";

    [Test]
    public async Task AJobRun_PublishesASpanAndItsDuration()
    {
        // Arrange
        ConcurrentBag<string> spans = [];
        ConcurrentBag<string> instruments = [];
        using ActivityListener activities = new()
        {
            ShouldListenTo = static source => source.Name == QuartzInstrumentationName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => spans.Add(activity.OperationName)
        };
        ActivitySource.AddActivityListener(activities);

        using MeterListener meters = new();
        meters.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == QuartzInstrumentationName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meters.SetMeasurementEventCallback<double>((instrument, _, _, _) => instruments.Add(instrument.Name));
        meters.Start();

        ServiceCollection services = new();
        services.AddLogging();
        NameValueCollection properties = new() { ["quartz.scheduler.instanceName"] = $"telemetry-{Guid.NewGuid()}" };
        services.AddQuartz(properties, static quartz =>
            quartz.AddJob<RecordingJob>(static job => job.WithIdentity("telemetry").StoreDurably()));
        await using ServiceProvider provider = services.BuildServiceProvider();
        IScheduler scheduler = await provider.GetRequiredService<ISchedulerFactory>().GetScheduler(CancellationToken);
        await scheduler.Start(CancellationToken);

        // Act
        await scheduler.TriggerJob(new JobKey("telemetry"), null, CancellationToken);

        // Assert
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        timeout.CancelAfter(TestTimeouts.Wait);
        while (!spans.Contains("Quartz.Job.Execute") || !instruments.Contains("quartz.job.execution.duration"))
        {
            meters.RecordObservableInstruments();
            await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        }

        await scheduler.Shutdown(waitForJobsToComplete: true, CancellationToken);
        await Assert.That(spans).Contains("Quartz.Job.Execute");
    }
}
