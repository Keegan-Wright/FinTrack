using System.Diagnostics.Metrics;

namespace FinanceTracker.Telemetry;

/// <summary>
/// FinTrack's own metrics, on the <see cref="MeterName"/> meter the service defaults export. Counts and timings only,
/// tagged by outcome or kind.
/// </summary>
public sealed class FinTrackMetrics
{
    /// <summary>
    /// The meter FinTrack's own metrics are on.
    /// </summary>
    public const string MeterName = "FinanceTracker";

    /// <summary>
    /// The metric's name. How long each bank connection took to sync.
    /// </summary>
    public const string ConnectionSyncDurationName = "fintrack.sync.connection.duration";
    /// <summary>
    /// The metric's name. Transactions a sync added or updated.
    /// </summary>
    public const string TransactionsSyncedName = "fintrack.sync.transactions";
    /// <summary>
    /// The metric's name. Fetches from the bank that failed.
    /// </summary>
    public const string FetchesFailedName = "fintrack.sync.fetches.failed";
    /// <summary>
    /// The metric's name. Notifications raised, by kind.
    /// </summary>
    public const string NotificationsRaisedName = "fintrack.notifications.raised";
    /// <summary>
    /// The metric's name. Notifications sent on by email, webhook or push.
    /// </summary>
    public const string NotificationsDeliveredName = "fintrack.notifications.delivered";
    /// <summary>
    /// The metric's name. Emails sent through the mail server.
    /// </summary>
    public const string EmailsSentName = "fintrack.emails.sent";
    /// <summary>
    /// The metric's name. Push devices removed because their browser had unsubscribed.
    /// </summary>
    public const string PushDevicesRemovedName = "fintrack.push.devices_removed";
    /// <summary>
    /// The metric's name. Bank connections by state.
    /// </summary>
    public const string ConnectionsName = "fintrack.connections";
    /// <summary>
    /// The metric's name. How long ago the connection that's waited longest last synced.
    /// </summary>
    public const string OldestSyncAgeName = "fintrack.sync.oldest_age";
    /// <summary>
    /// The metric's name. How long calls to TrueLayer queued for the rate limit.
    /// </summary>
    public const string TrueLayerThrottleWaitName = "fintrack.truelayer.throttle.wait";
    /// <summary>
    /// The metric's name. Single sign-on attempts that didn't sign anyone in.
    /// </summary>
    public const string SingleSignOnFailuresName = "fintrack.sso.failures";
    /// <summary>
    /// The metric's name. Authenticator codes refused for having been used already.
    /// </summary>
    public const string AuthenticatorCodesReusedName = "fintrack.authenticator.codes_reused";
    /// <summary>
    /// The metric's name. Transactions the user's rules changed.
    /// </summary>
    public const string RulesChangedName = "fintrack.rules.transactions_changed";
    /// <summary>
    /// The metric's name. Transactions newly decided by transfer matching.
    /// </summary>
    public const string TransferDecisionsName = "fintrack.transfers.decisions";
    /// <summary>
    /// The metric's name. Bills added, ended or following a price change.
    /// </summary>
    public const string BillChangesName = "fintrack.bills.changes";
    /// <summary>
    /// The metric's name. Transactions whose bill changed.
    /// </summary>
    public const string BillTransactionsMatchedName = "fintrack.bills.transactions_matched";
    /// <summary>
    /// The metric's name. New subscriptions suggested.
    /// </summary>
    public const string SubscriptionsSuggestedName = "fintrack.subscriptions.suggested";
    /// <summary>
    /// The metric's name. Balance snapshots refreshed.
    /// </summary>
    public const string SnapshotsRefreshedName = "fintrack.balances.snapshots_refreshed";

    /// <summary>
    /// A tag value. The outcome when it worked.
    /// </summary>
    private const string Succeeded = "succeeded";
    /// <summary>
    /// A tag value. The outcome when it didn't.
    /// </summary>
    private const string Failed = "failed";
    /// <summary>
    /// A tag value. A transaction or bill that's new.
    /// </summary>
    private const string Added = "added";
    /// <summary>
    /// A tag value. A transaction that changed.
    /// </summary>
    private const string Updated = "updated";
    /// <summary>
    /// A tag value. The email channel.
    /// </summary>
    private const string Email = "email";
    /// <summary>
    /// A tag value. The webhook channel.
    /// </summary>
    private const string Webhook = "webhook";
    /// <summary>
    /// A tag value. The push channel.
    /// </summary>
    private const string Push = "push";
    /// <summary>
    /// A tag value. A connection that's fine.
    /// </summary>
    private const string Ok = "ok";
    /// <summary>
    /// A tag value. A connection whose consent ends soon.
    /// </summary>
    private const string RenewalDue = "renewal_due";
    /// <summary>
    /// A tag value. A connection whose consent has ended.
    /// </summary>
    private const string Lapsed = "lapsed";
    /// <summary>
    /// A tag value. A connection whose syncs are failing.
    /// </summary>
    private const string Failing = "failing";
    /// <summary>
    /// A tag value. A transaction decided to be a transfer.
    /// </summary>
    private const string Transfer = "transfer";
    /// <summary>
    /// A tag value. A transaction put up for review.
    /// </summary>
    private const string ToReview = "to_review";
    /// <summary>
    /// A tag value. A transaction cleared of an earlier decision.
    /// </summary>
    private const string Cleared = "cleared";
    /// <summary>
    /// A tag value. A bill that ended.
    /// </summary>
    private const string Ended = "ended";
    /// <summary>
    /// A tag value. A bill following a price change.
    /// </summary>
    private const string PriceChanged = "price_changed";

    // A sync takes seconds to minutes, the default buckets are for milliseconds
    private static readonly double[] SyncDurationBuckets = [0.5, 1, 2, 5, 10, 20, 30, 60, 120, 300];

    // Most calls don't wait at all, a busy sync's queue holds them for seconds
    private static readonly double[] ThrottleWaitBuckets = [0, 0.05, 0.1, 0.25, 0.5, 1, 2, 5, 10, 30, 60];

    private readonly Histogram<double> _connectionSyncDuration;
    private readonly Counter<long> _transactionsSynced;
    private readonly Counter<long> _fetchesFailed;
    private readonly Counter<long> _notificationsRaised;
    private readonly Counter<long> _notificationsDelivered;
    private readonly Counter<long> _emailsSent;
    private readonly Counter<long> _pushDevicesRemoved;
    private readonly Gauge<long> _connections;
    private readonly Histogram<double> _trueLayerThrottleWait;
    private readonly Counter<long> _singleSignOnFailures;
    private readonly Counter<long> _authenticatorCodesReused;
    private readonly Counter<long> _rulesChanged;
    private readonly Counter<long> _transferDecisions;
    private readonly Counter<long> _billChanges;
    private readonly Counter<long> _billTransactionsMatched;
    private readonly Counter<long> _subscriptionsSuggested;
    private readonly Counter<long> _snapshotsRefreshed;
    private readonly TimeProvider _timeProvider;

    // When the connection that's waited longest for a sync last synced, read when the age gauge is collected
    private DateTimeOffset? _oldestSyncedAt;

    /// <summary>
    /// Creates the metrics on the <see cref="MeterName"/> meter.
    /// </summary>
    /// <param name="meterFactory">Makes the meter.</param>
    /// <param name="timeProvider">The clock the oldest sync's age is counted on.</param>
    public FinTrackMetrics(IMeterFactory meterFactory, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        _timeProvider = timeProvider;
        Meter meter = meterFactory.Create(MeterName);

        _connectionSyncDuration = meter.CreateHistogram(ConnectionSyncDurationName, "s",
            "How long each bank connection took to sync, by whether it succeeded",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = SyncDurationBuckets });
        _transactionsSynced = meter.CreateCounter<long>(TransactionsSyncedName, "{transaction}",
            "Transactions a sync brought in, added for the first time or updated");
        _fetchesFailed = meter.CreateCounter<long>(FetchesFailedName, "{fetch}",
            "Fetches from the bank that failed, the rest of the sync carrying on without that data");
        _notificationsRaised = meter.CreateCounter<long>(NotificationsRaisedName, "{notification}",
            "Notifications raised, by kind");
        _notificationsDelivered = meter.CreateCounter<long>(NotificationsDeliveredName, "{notification}",
            "Notifications sent on by email, webhook or push, by whether they got there");
        _pushDevicesRemoved = meter.CreateCounter<long>(PushDevicesRemovedName, "{device}",
            "Push devices removed because the push service said their browser had unsubscribed");
        _emailsSent = meter.CreateCounter<long>(EmailsSentName, "{email}",
            "Emails sent through the mail server, notifications and account emails alike, by whether it took them");
        _connections = meter.CreateGauge<long>(ConnectionsName, "{connection}",
            "Bank connections by state: ok, renewal due, consent lapsed, or failing to sync");
        meter.CreateObservableGauge(OldestSyncAgeName, ObserveOldestSyncAge, "s",
            "How long ago the connection that's waited longest last synced, climbing while nothing syncs");
        _trueLayerThrottleWait = meter.CreateHistogram(TrueLayerThrottleWaitName, "s",
            "How long calls to TrueLayer's data API queued for FinTrack's own rate limit before going",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = ThrottleWaitBuckets });
        _singleSignOnFailures = meter.CreateCounter<long>(SingleSignOnFailuresName, "{sign_in}",
            "Single sign-on attempts that didn't sign anyone in, by why. Identity's own metrics only see the sign "
            + "ins that work");
        _authenticatorCodesReused = meter.CreateCounter<long>(AuthenticatorCodesReusedName, "{code}",
            "Authenticator codes refused for having been used already, or being older than one that was. Someone may "
            + "have watched it typed or passed it on. Identity's own metrics count them as failures");
        _rulesChanged = meter.CreateCounter<long>(RulesChangedName, "{transaction}",
            "Transactions the user's rules changed, categorising, hiding or marking them");
        _transferDecisions = meter.CreateCounter<long>(TransferDecisionsName, "{transaction}",
            "Transactions newly decided to be transfers, put up for review, or cleared of an earlier decision");
        _billChanges = meter.CreateCounter<long>(BillChangesName, "{bill}",
            "Bills added from new standing orders and direct debits, ended, or following a price change");
        _billTransactionsMatched = meter.CreateCounter<long>(BillTransactionsMatchedName, "{transaction}",
            "Transactions whose bill changed, newly paying one or no longer");
        _subscriptionsSuggested = meter.CreateCounter<long>(SubscriptionsSuggestedName, "{subscription}",
            "New subscriptions suggested from repeating payments");
        _snapshotsRefreshed = meter.CreateCounter<long>(SnapshotsRefreshedName, "{snapshot}",
            "Account balance snapshots refreshed for the day, the history the net worth chart draws");
    }

    /// <summary>
    /// One bank connection's sync, from starting to saving or failing.
    /// </summary>
    /// <param name="took">How long it took.</param>
    /// <param name="succeeded">Whether it saved.</param>
    public void ConnectionSynced(TimeSpan took, bool succeeded) =>
        _connectionSyncDuration.Record(took.TotalSeconds, OutcomeTag(succeeded));

    /// <summary>
    /// What one connection's sync brought in.
    /// </summary>
    /// <param name="added">Transactions added for the first time.</param>
    /// <param name="updated">Transactions updated.</param>
    public void TransactionsSynced(int added, int updated)
    {
        _transactionsSynced.Add(added, new KeyValuePair<string, object?>(TelemetryTags.Change, Added));
        _transactionsSynced.Add(updated, new KeyValuePair<string, object?>(TelemetryTags.Change, Updated));
    }

    /// <summary>
    /// Fetches from the bank that failed in one sync.
    /// </summary>
    /// <param name="count">How many failed.</param>
    public void FetchesFailed(int count) => _fetchesFailed.Add(count);

    /// <summary>
    /// A notification raised.
    /// </summary>
    /// <param name="kind">What it's about.</param>
    public void NotificationRaised(string kind) =>
        _notificationsRaised.Add(1, new KeyValuePair<string, object?>(TelemetryTags.NotificationKind, kind));

    /// <summary>
    /// A notification sent by email.
    /// </summary>
    /// <param name="succeeded">Whether the mail server took it.</param>
    public void NotificationEmailed(bool succeeded) => Delivered(Email, succeeded);

    /// <summary>
    /// A notification posted to a webhook.
    /// </summary>
    /// <param name="succeeded">Whether it got there.</param>
    public void NotificationPostedToWebhook(bool succeeded) => Delivered(Webhook, succeeded);

    /// <summary>
    /// A notification pushed to one of a user's devices.
    /// </summary>
    /// <param name="succeeded">Whether the push service took it.</param>
    public void NotificationPushed(bool succeeded) => Delivered(Push, succeeded);

    /// <summary>
    /// A push device removed because its browser had unsubscribed.
    /// </summary>
    public void PushDeviceRemoved() => _pushDevicesRemoved.Add(1);

    /// <summary>
    /// An email sent through the mail server, a notification or an account email.
    /// </summary>
    /// <param name="succeeded">Whether the mail server took it.</param>
    public void EmailSent(bool succeeded) => _emailsSent.Add(1, OutcomeTag(succeeded));

    /// <summary>
    /// How many bank connections are in each state, every connection counted once in the first that fits: lapsed,
    /// failing, renewal due, ok.
    /// </summary>
    /// <param name="ok">Connections that are fine.</param>
    /// <param name="renewalDue">Connections whose consent ends soon.</param>
    /// <param name="lapsed">Connections whose consent has ended.</param>
    /// <param name="failing">Connections whose syncs are failing.</param>
    public void ConnectionsCounted(int ok, int renewalDue, int lapsed, int failing)
    {
        _connections.Record(ok, StateTag(Ok));
        _connections.Record(renewalDue, StateTag(RenewalDue));
        _connections.Record(lapsed, StateTag(Lapsed));
        _connections.Record(failing, StateTag(Failing));
    }

    /// <summary>
    /// When the connection that's waited longest last synced, null when none has synced. The age gauge counts up from
    /// it each time it's collected, so it keeps climbing if syncing stops.
    /// </summary>
    /// <param name="syncedAt">When it last synced, null when none has.</param>
    public void OldestSyncFound(DateTimeOffset? syncedAt) => _oldestSyncedAt = syncedAt;

    /// <summary>
    /// How long a call to TrueLayer queued for the rate limit, nothing for most.
    /// </summary>
    /// <param name="waited">How long it queued.</param>
    public void TrueLayerThrottled(TimeSpan waited) => _trueLayerThrottleWait.Record(waited.TotalSeconds);

    /// <summary>
    /// A single sign-on attempt that ended back on the sign in page, or at the lockout page, rather than signed in.
    /// </summary>
    /// <param name="reason">Why it failed.</param>
    public void SingleSignOnFailed(SingleSignOnFailure reason) =>
        _singleSignOnFailures.Add(1,
            new KeyValuePair<string, object?>(TelemetryTags.SingleSignOnFailureReason, ReasonTag(reason)));

    /// <summary>
    /// An authenticator code that was right but had been used already, or was older than one that was, so was refused.
    /// </summary>
    public void AuthenticatorCodeReused() => _authenticatorCodesReused.Add(1);

    /// <summary>
    /// What one run of a user's rules changed.
    /// </summary>
    /// <param name="transactionsChanged">How many transactions changed.</param>
    public void RulesApplied(int transactionsChanged) => _rulesChanged.Add(transactionsChanged);

    /// <summary>
    /// What one run of transfer matching newly decided.
    /// </summary>
    /// <param name="transfers">Transactions decided to be transfers.</param>
    /// <param name="toReview">Transactions put up for review.</param>
    /// <param name="cleared">Transactions cleared of an earlier decision.</param>
    public void TransfersMatched(int transfers, int toReview, int cleared)
    {
        _transferDecisions.Add(transfers, DecisionTag(Transfer));
        _transferDecisions.Add(toReview, DecisionTag(ToReview));
        _transferDecisions.Add(cleared, DecisionTag(Cleared));
    }

    /// <summary>
    /// What one sync of a user's bills changed.
    /// </summary>
    /// <param name="added">Bills added from new standing orders and direct debits.</param>
    /// <param name="ended">Bills ended.</param>
    /// <param name="pricesChanged">Bills following a price change.</param>
    /// <param name="transactionsMatched">Transactions whose bill changed.</param>
    /// <param name="subscriptionsSuggested">New subscriptions suggested.</param>
    public void BillsSynced(int added, int ended, int pricesChanged, int transactionsMatched,
        int subscriptionsSuggested)
    {
        _billChanges.Add(added, BillChangeTag(Added));
        _billChanges.Add(ended, BillChangeTag(Ended));
        _billChanges.Add(pricesChanged, BillChangeTag(PriceChanged));
        _billTransactionsMatched.Add(transactionsMatched);
        _subscriptionsSuggested.Add(subscriptionsSuggested);
    }

    /// <summary>
    /// Balance snapshots refreshed for one user's day.
    /// </summary>
    /// <param name="refreshed">How many were refreshed.</param>
    public void SnapshotsRefreshed(int refreshed) => _snapshotsRefreshed.Add(refreshed);

    /// <summary>
    /// The transfer decision tag.
    /// </summary>
    /// <param name="decision">What was decided.</param>
    private static KeyValuePair<string, object?> DecisionTag(string decision) =>
        new(TelemetryTags.TransferDecision, decision);

    /// <summary>
    /// The bill change tag.
    /// </summary>
    /// <param name="change">How the bill changed.</param>
    private static KeyValuePair<string, object?> BillChangeTag(string change) =>
        new(TelemetryTags.BillChange, change);

    /// <summary>
    /// The tag value for why a single sign-on failed.
    /// </summary>
    /// <param name="reason">Why it failed.</param>
    private static string ReasonTag(SingleSignOnFailure reason)
    {
        switch (reason)
        {
            case SingleSignOnFailure.NotStarted:
                return "not_started";
            case SingleSignOnFailure.Provider:
                return "provider";
            case SingleSignOnFailure.NoIdentity:
                return "no_identity";
            case SingleSignOnFailure.NoAccount:
                return "no_account";
            case SingleSignOnFailure.LockedOut:
                return "locked_out";
            default:
                throw new ArgumentOutOfRangeException(nameof(reason), reason, null);
        }
    }

    /// <summary>
    /// How long ago the connection that's waited longest last synced, in seconds. Nothing when none has synced.
    /// </summary>
    private IEnumerable<Measurement<double>> ObserveOldestSyncAge()
    {
        if (_oldestSyncedAt is not { } syncedAt)
        {
            return [];
        }

        return [new Measurement<double>((_timeProvider.GetUtcNow() - syncedAt).TotalSeconds)];
    }

    /// <summary>
    /// The connection state tag.
    /// </summary>
    /// <param name="state">The connection's state.</param>
    private static KeyValuePair<string, object?> StateTag(string state) =>
        new(TelemetryTags.ConnectionState, state);

    /// <summary>
    /// A notification sent on.
    /// </summary>
    /// <param name="channel">How it was sent.</param>
    /// <param name="succeeded">Whether it got there.</param>
    private void Delivered(string channel, bool succeeded) =>
        _notificationsDelivered.Add(1, new KeyValuePair<string, object?>(TelemetryTags.NotificationChannel, channel),
            OutcomeTag(succeeded));

    /// <summary>
    /// The outcome tag.
    /// </summary>
    /// <param name="succeeded">Whether it worked.</param>
    private static KeyValuePair<string, object?> OutcomeTag(bool succeeded)
    {
        if (succeeded)
        {
            return new KeyValuePair<string, object?>(TelemetryTags.Outcome, Succeeded);
        }

        return new KeyValuePair<string, object?>(TelemetryTags.Outcome, Failed);
    }
}
