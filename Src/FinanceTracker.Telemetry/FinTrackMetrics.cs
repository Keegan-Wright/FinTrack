using System.Diagnostics.Metrics;

namespace FinanceTracker.Telemetry;

/// <summary>
/// FinTrack's own metrics, on the <see cref="MeterName"/> meter the service defaults export. Counts and timings only,
/// tagged by outcome or kind.
/// </summary>
public sealed class FinTrackMetrics
{
    public const string MeterName = "FinanceTracker";

    public const string ConnectionSyncDurationName = "fintrack.sync.connection.duration";
    public const string TransactionsSyncedName = "fintrack.sync.transactions";
    public const string FetchesFailedName = "fintrack.sync.fetches.failed";
    public const string NotificationsRaisedName = "fintrack.notifications.raised";
    public const string NotificationsDeliveredName = "fintrack.notifications.delivered";
    public const string EmailsSentName = "fintrack.emails.sent";
    public const string ConnectionsName = "fintrack.connections";
    public const string OldestSyncAgeName = "fintrack.sync.oldest_age";
    public const string TrueLayerThrottleWaitName = "fintrack.truelayer.throttle.wait";
    public const string SingleSignOnFailuresName = "fintrack.sso.failures";
    public const string RulesChangedName = "fintrack.rules.transactions_changed";
    public const string TransferDecisionsName = "fintrack.transfers.decisions";
    public const string BillChangesName = "fintrack.bills.changes";
    public const string BillTransactionsMatchedName = "fintrack.bills.transactions_matched";
    public const string SubscriptionsSuggestedName = "fintrack.subscriptions.suggested";
    public const string SnapshotsRefreshedName = "fintrack.balances.snapshots_refreshed";

    private const string Succeeded = "succeeded";
    private const string Failed = "failed";
    private const string Added = "added";
    private const string Updated = "updated";
    private const string Email = "email";
    private const string Webhook = "webhook";
    private const string Ok = "ok";
    private const string RenewalDue = "renewal_due";
    private const string Lapsed = "lapsed";
    private const string Failing = "failing";
    private const string Transfer = "transfer";
    private const string ToReview = "to_review";
    private const string Cleared = "cleared";
    private const string Ended = "ended";
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
    private readonly Gauge<long> _connections;
    private readonly Histogram<double> _trueLayerThrottleWait;
    private readonly Counter<long> _singleSignOnFailures;
    private readonly Counter<long> _rulesChanged;
    private readonly Counter<long> _transferDecisions;
    private readonly Counter<long> _billChanges;
    private readonly Counter<long> _billTransactionsMatched;
    private readonly Counter<long> _subscriptionsSuggested;
    private readonly Counter<long> _snapshotsRefreshed;
    private readonly TimeProvider _timeProvider;

    // When the connection that's waited longest for a sync last synced, read when the age gauge is collected
    private DateTimeOffset? _oldestSyncedAt;

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
            "Notifications sent on by email or webhook, by whether they got there");
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
    public void ConnectionSynced(TimeSpan took, bool succeeded) =>
        _connectionSyncDuration.Record(took.TotalSeconds, OutcomeTag(succeeded));

    /// <summary>
    /// What one connection's sync brought in.
    /// </summary>
    public void TransactionsSynced(int added, int updated)
    {
        _transactionsSynced.Add(added, new KeyValuePair<string, object?>(TelemetryTags.Change, Added));
        _transactionsSynced.Add(updated, new KeyValuePair<string, object?>(TelemetryTags.Change, Updated));
    }

    public void FetchesFailed(int count) => _fetchesFailed.Add(count);

    public void NotificationRaised(string kind) =>
        _notificationsRaised.Add(1, new KeyValuePair<string, object?>(TelemetryTags.NotificationKind, kind));

    public void NotificationEmailed(bool succeeded) => Delivered(Email, succeeded);

    public void NotificationPostedToWebhook(bool succeeded) => Delivered(Webhook, succeeded);

    public void EmailSent(bool succeeded) => _emailsSent.Add(1, OutcomeTag(succeeded));

    /// <summary>
    /// How many bank connections are in each state, every connection counted once in the first that fits: lapsed,
    /// failing, renewal due, ok.
    /// </summary>
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
    public void OldestSyncFound(DateTimeOffset? syncedAt) => _oldestSyncedAt = syncedAt;

    /// <summary>
    /// How long a call to TrueLayer queued for the rate limit, nothing for most.
    /// </summary>
    public void TrueLayerThrottled(TimeSpan waited) => _trueLayerThrottleWait.Record(waited.TotalSeconds);

    /// <summary>
    /// A single sign-on attempt that ended back on the sign in page, or at the lockout page, rather than signed in.
    /// </summary>
    public void SingleSignOnFailed(SingleSignOnFailure reason) =>
        _singleSignOnFailures.Add(1,
            new KeyValuePair<string, object?>(TelemetryTags.SingleSignOnFailureReason, ReasonTag(reason)));

    /// <summary>
    /// What one run of a user's rules changed.
    /// </summary>
    public void RulesApplied(int transactionsChanged) => _rulesChanged.Add(transactionsChanged);

    /// <summary>
    /// What one run of transfer matching newly decided.
    /// </summary>
    public void TransfersMatched(int transfers, int toReview, int cleared)
    {
        _transferDecisions.Add(transfers, DecisionTag(Transfer));
        _transferDecisions.Add(toReview, DecisionTag(ToReview));
        _transferDecisions.Add(cleared, DecisionTag(Cleared));
    }

    /// <summary>
    /// What one sync of a user's bills changed.
    /// </summary>
    public void BillsSynced(int added, int ended, int pricesChanged, int transactionsMatched, int subscriptionsSuggested)
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
    public void SnapshotsRefreshed(int refreshed) => _snapshotsRefreshed.Add(refreshed);

    private static KeyValuePair<string, object?> DecisionTag(string decision) =>
        new(TelemetryTags.TransferDecision, decision);

    private static KeyValuePair<string, object?> BillChangeTag(string change) =>
        new(TelemetryTags.BillChange, change);

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

    private IEnumerable<Measurement<double>> ObserveOldestSyncAge()
    {
        if (_oldestSyncedAt is not { } syncedAt)
        {
            return [];
        }

        return [new Measurement<double>((_timeProvider.GetUtcNow() - syncedAt).TotalSeconds)];
    }

    private static KeyValuePair<string, object?> StateTag(string state) =>
        new(TelemetryTags.ConnectionState, state);

    private void Delivered(string channel, bool succeeded) =>
        _notificationsDelivered.Add(1, new KeyValuePair<string, object?>(TelemetryTags.NotificationChannel, channel),
            OutcomeTag(succeeded));

    private static KeyValuePair<string, object?> OutcomeTag(bool succeeded)
    {
        if (succeeded)
        {
            return new KeyValuePair<string, object?>(TelemetryTags.Outcome, Succeeded);
        }

        return new KeyValuePair<string, object?>(TelemetryTags.Outcome, Failed);
    }
}
