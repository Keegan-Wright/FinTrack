using FinanceTracker.Telemetry;

namespace FinanceTracker.Tests.Unit.Infrastructure.Telemetry;

/// <summary>
/// The names FinTrack's telemetry goes out under, written out here once. Tempo queries, dashboards and alerts use
/// them, so renaming one should be a decision, not something a refactor does quietly.
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
    [Arguments(TelemetryTags.NotificationKind, "fintrack.notification.kind")]
    [Arguments(TelemetryTags.NotificationId, "fintrack.notification.id")]
    public async Task Tag_GoesOutUnderItsName(string tag, string name) =>
        await Assert.That(tag).IsEqualTo(name);

    [Test]
    public async Task ActivitySources_GoOutUnderTheirNames() =>
        await Assert.That(FinTrackActivitySources.All).IsEquivalentTo(["FinanceTracker", "FinanceTracker.Infrastructure"]);

    [Test]
    public async Task EveryTag_HasItsNameWrittenOutHere()
    {
        // A tag added to TelemetryTags needs a row above too
        int tags = typeof(TelemetryTags).GetFields().Count(static x => x.IsLiteral);

        await Assert.That(tags).IsEqualTo(34);
    }
}
