using FinanceTracker.Telemetry;

namespace FinanceTracker.Tests.Unit.Infrastructure.Telemetry;

/// <summary>
/// The names FinTrack's telemetry goes out under, written out here once. Tempo and Prometheus queries, dashboards and
/// alerts use them, so renaming one should be a decision, not something a refactor does quietly.
/// </summary>
public class TelemetryNamesTests
{
    [Test]
    [Arguments(TelemetryTags.UserId, "user.id")]
    [Arguments(TelemetryTags.ConnectionId, "fintrack.connection.id")]
    [Arguments(TelemetryTags.ConnectionCount, "fintrack.connection.count")]
    [Arguments(TelemetryTags.AccountId, "fintrack.account.id")]
    [Arguments(TelemetryTags.ItemCount, "fintrack.items")]
    [Arguments(TelemetryTags.TrueLayerTraceId, "truelayer.trace_id")]
    [Arguments(TelemetryTags.Accounts, "fintrack.accounts")]
    [Arguments(TelemetryTags.Cards, "fintrack.cards")]
    [Arguments(TelemetryTags.Days, "fintrack.days")]
    [Arguments(TelemetryTags.TransactionsAdded, "fintrack.transactions.added")]
    [Arguments(TelemetryTags.TransactionsUpdated, "fintrack.transactions.updated")]
    [Arguments(TelemetryTags.TransactionsConsidered, "fintrack.transactions.considered")]
    [Arguments(TelemetryTags.TransactionsChanged, "fintrack.transactions.changed")]
    [Arguments(TelemetryTags.PendingRemoved, "fintrack.pending.removed")]
    [Arguments(TelemetryTags.FetchesFailed, "fintrack.fetches.failed")]
    [Arguments(TelemetryTags.Rules, "fintrack.rules")]
    [Arguments(TelemetryTags.RulesMatched, "fintrack.rules.matched")]
    [Arguments(TelemetryTags.Transfers, "fintrack.transfers")]
    [Arguments(TelemetryTags.TransfersToReview, "fintrack.transfers.to_review")]
    [Arguments(TelemetryTags.TransfersCleared, "fintrack.transfers.cleared")]
    [Arguments(TelemetryTags.BillsAdded, "fintrack.bills.added")]
    [Arguments(TelemetryTags.BillsEnded, "fintrack.bills.ended")]
    [Arguments(TelemetryTags.BillsMatched, "fintrack.bills.matched")]
    [Arguments(TelemetryTags.BillsPricesChanged, "fintrack.bills.prices_changed")]
    [Arguments(TelemetryTags.SubscriptionsSuggested, "fintrack.subscriptions.suggested")]
    [Arguments(TelemetryTags.PayIncomes, "fintrack.pay.incomes")]
    [Arguments(TelemetryTags.PayDays, "fintrack.pay.days")]
    [Arguments(TelemetryTags.PayChosen, "fintrack.pay.chosen")]
    [Arguments(TelemetryTags.PayOff, "fintrack.pay.off")]
    [Arguments(TelemetryTags.SnapshotsRefreshed, "fintrack.snapshots.refreshed")]
    [Arguments(TelemetryTags.SnapshotsStale, "fintrack.snapshots.stale")]
    [Arguments(TelemetryTags.NotificationsRaised, "fintrack.notifications.raised")]
    [Arguments(TelemetryTags.Outcome, "fintrack.outcome")]
    [Arguments(TelemetryTags.Change, "fintrack.transactions.change")]
    [Arguments(TelemetryTags.ConnectionState, "fintrack.connection.state")]
    [Arguments(TelemetryTags.NotificationChannel, "fintrack.notification.channel")]
    [Arguments(TelemetryTags.NotificationKind, "fintrack.notification.kind")]
    [Arguments(TelemetryTags.NotificationId, "fintrack.notification.id")]
    [Arguments(TelemetryTags.SingleSignOnFailureReason, "fintrack.sso.failure")]
    public async Task Tag_GoesOutUnderItsName(string tag, string name) =>
        await Assert.That(tag).IsEqualTo(name);

    [Test]
    [Arguments(FinTrackMetrics.ConnectionSyncDurationName, "fintrack.sync.connection.duration")]
    [Arguments(FinTrackMetrics.TransactionsSyncedName, "fintrack.sync.transactions")]
    [Arguments(FinTrackMetrics.FetchesFailedName, "fintrack.sync.fetches.failed")]
    [Arguments(FinTrackMetrics.NotificationsRaisedName, "fintrack.notifications.raised")]
    [Arguments(FinTrackMetrics.NotificationsDeliveredName, "fintrack.notifications.delivered")]
    [Arguments(FinTrackMetrics.EmailsSentName, "fintrack.emails.sent")]
    [Arguments(FinTrackMetrics.ConnectionsName, "fintrack.connections")]
    [Arguments(FinTrackMetrics.OldestSyncAgeName, "fintrack.sync.oldest_age")]
    [Arguments(FinTrackMetrics.TrueLayerThrottleWaitName, "fintrack.truelayer.throttle.wait")]
    [Arguments(FinTrackMetrics.SingleSignOnFailuresName, "fintrack.sso.failures")]
    public async Task Instrument_GoesOutUnderItsName(string instrument, string name) =>
        await Assert.That(instrument).IsEqualTo(name);

    [Test]
    public async Task SourcesAndMeter_GoOutUnderTheirNames()
    {
        using (Assert.Multiple())
        {
            await Assert.That(FinTrackActivitySources.All)
                .IsEquivalentTo(["FinanceTracker", "FinanceTracker.Infrastructure"]);
            await Assert.That(FinTrackMetrics.MeterName).IsEqualTo("FinanceTracker");
        }
    }

    [Test]
    public async Task EveryName_IsWrittenOutHere()
    {
        // A tag or instrument added needs a row above too
        int tags = typeof(TelemetryTags).GetFields().Count(static x => x.IsLiteral);
        int instruments = typeof(FinTrackMetrics).GetFields()
            .Count(static x => x.IsLiteral && x.Name.EndsWith("Name", StringComparison.Ordinal)) - 1;

        using (Assert.Multiple())
        {
            await Assert.That(tags).IsEqualTo(39);
            await Assert.That(instruments).IsEqualTo(10);
        }
    }
}
