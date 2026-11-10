using FinanceTracker.Telemetry;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace FinanceTracker.Tests.Unit.Infrastructure.Telemetry;

public class FinTrackMetricsTests
{
    [Test]
    public async Task ConnectionSynced_RecordsHowLongInSecondsByOutcome()
    {
        // Arrange
        using TestMetrics metrics = new();
        using MetricCollector<double> durations = metrics.Collect<double>(FinTrackMetrics.ConnectionSyncDurationName);

        // Act
        metrics.Metrics.ConnectionSynced(TimeSpan.FromSeconds(2.5), succeeded: true);
        metrics.Metrics.ConnectionSynced(TimeSpan.FromSeconds(4), succeeded: false);

        // Assert
        IReadOnlyList<CollectedMeasurement<double>> recorded = durations.GetMeasurementSnapshot();
        using (Assert.Multiple())
        {
            await Assert.That(recorded.Select(static x => x.Value)).IsEquivalentTo([2.5, 4]);
            await Assert.That(recorded[0].Tags[TelemetryTags.Outcome]).IsEqualTo("succeeded");
            await Assert.That(recorded[1].Tags[TelemetryTags.Outcome]).IsEqualTo("failed");
        }
    }

    [Test]
    public async Task TransactionsSynced_CountsAddedAndUpdatedApart()
    {
        // Arrange
        using TestMetrics metrics = new();
        using MetricCollector<long> transactions = metrics.Collect<long>(FinTrackMetrics.TransactionsSyncedName);

        // Act
        metrics.Metrics.TransactionsSynced(added: 3, updated: 5);

        // Assert
        Dictionary<string, long> byChange = transactions.GetMeasurementSnapshot()
            .ToDictionary(static x => x.Tags[TelemetryTags.Change]!.ToString()!, static x => x.Value);
        await Assert.That(byChange).IsEquivalentTo(new Dictionary<string, long> { ["added"] = 3, ["updated"] = 5 });
    }

    [Test]
    public async Task NotificationDelivered_TagsTheChannelAndOutcome()
    {
        // Arrange
        using TestMetrics metrics = new();
        using MetricCollector<long> delivered = metrics.Collect<long>(FinTrackMetrics.NotificationsDeliveredName);

        // Act
        metrics.Metrics.NotificationEmailed(succeeded: true);
        metrics.Metrics.NotificationPostedToWebhook(succeeded: false);

        // Assert
        IReadOnlyList<CollectedMeasurement<long>> recorded = delivered.GetMeasurementSnapshot();
        using (Assert.Multiple())
        {
            await Assert.That(recorded[0].Tags[TelemetryTags.NotificationChannel]).IsEqualTo("email");
            await Assert.That(recorded[0].Tags[TelemetryTags.Outcome]).IsEqualTo("succeeded");
            await Assert.That(recorded[1].Tags[TelemetryTags.NotificationChannel]).IsEqualTo("webhook");
            await Assert.That(recorded[1].Tags[TelemetryTags.Outcome]).IsEqualTo("failed");
        }
    }

    [Test]
    public async Task OldestSyncAge_IsWorkedOutWhenCollected()
    {
        // Arrange, found at 08:00 and collected at 11:00 with nothing synced between
        DateTimeOffset syncedAt = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);
        using TestMetrics metrics = new(new FixedTimeProvider(syncedAt.UtcDateTime.AddHours(3)));
        using MetricCollector<double> age = metrics.Collect<double>(FinTrackMetrics.OldestSyncAgeName);
        metrics.Metrics.OldestSyncFound(syncedAt);

        // Act
        age.RecordObservableInstruments();

        // Assert, so it keeps climbing if syncing stops rather than holding the last value recorded
        await Assert.That(age.LastMeasurement!.Value).IsEqualTo(TimeSpan.FromHours(3).TotalSeconds);
    }

    [Test]
    public async Task OldestSyncAge_NothingSynced_ReportsNothing()
    {
        // Arrange
        using TestMetrics metrics = new();
        using MetricCollector<double> age = metrics.Collect<double>(FinTrackMetrics.OldestSyncAgeName);
        metrics.Metrics.OldestSyncFound(null);

        // Act
        age.RecordObservableInstruments();

        // Assert
        await Assert.That(age.GetMeasurementSnapshot()).IsEmpty();
    }

    [Test]
    public async Task TrueLayerThrottled_RecordsTheWaitInSeconds()
    {
        // Arrange
        using TestMetrics metrics = new();
        using MetricCollector<double> waits = metrics.Collect<double>(FinTrackMetrics.TrueLayerThrottleWaitName);

        // Act
        metrics.Metrics.TrueLayerThrottled(TimeSpan.FromMilliseconds(250));

        // Assert
        await Assert.That(waits.LastMeasurement!.Value).IsEqualTo(0.25);
    }

    [Test]
    public async Task Metrics_NeverTagTheUser()
    {
        // Arrange
        using TestMetrics metrics = new();
        using MetricCollector<long> emails = metrics.Collect<long>(FinTrackMetrics.EmailsSentName);
        using MetricCollector<long> raised = metrics.Collect<long>(FinTrackMetrics.NotificationsRaisedName);

        // Act
        metrics.Metrics.EmailSent(succeeded: true);
        metrics.Metrics.NotificationRaised("BillDue");

        // Assert
        IEnumerable<string> tags = emails.GetMeasurementSnapshot().SelectMany(static x => x.Tags.Keys)
            .Concat(raised.GetMeasurementSnapshot().SelectMany(static x => x.Tags.Keys));
        await Assert.That(tags).DoesNotContain(TelemetryTags.UserId);
    }
}
