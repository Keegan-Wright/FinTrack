namespace FinanceTracker.Telemetry;

/// <summary>
/// The names of the attributes FinTrack puts on its spans, in one place so Tempo queries and dashboards can rely on
/// them. Ours start <c>fintrack.</c>.
/// </summary>
public static class TelemetryTags
{
    /// <summary>
    /// The user the work is for, OpenTelemetry's own name for it.
    /// </summary>
    public const string UserId = "user.id";

    /// <summary>
    /// The bank connection a span is for.
    /// </summary>
    public const string ConnectionId = "fintrack.connection.id";

    /// <summary>
    /// How many connections a sync job synced.
    /// </summary>
    public const string ConnectionCount = "fintrack.connection.count";

    /// <summary>
    /// The account or card a span is for.
    /// </summary>
    public const string AccountId = "fintrack.account.id";

    /// <summary>
    /// How many items a fetch from the bank returned.
    /// </summary>
    public const string ItemCount = "fintrack.items";

    /// <summary>
    /// TrueLayer's id for a request it refused, which their support can look up.
    /// </summary>
    public const string TrueLayerTraceId = "truelayer.trace_id";

    /// <summary>
    /// How many accounts the work covered.
    /// </summary>
    public const string Accounts = "fintrack.accounts";

    /// <summary>
    /// How many cards a connection's sync covered.
    /// </summary>
    public const string Cards = "fintrack.cards";

    /// <summary>
    /// How many days of balance history were worked out.
    /// </summary>
    public const string Days = "fintrack.days";

    /// <summary>
    /// Transactions a sync brought in for the first time.
    /// </summary>
    public const string TransactionsAdded = "fintrack.transactions.added";

    /// <summary>
    /// Transactions a sync brought in again and updated.
    /// </summary>
    public const string TransactionsUpdated = "fintrack.transactions.updated";

    /// <summary>
    /// Transactions the work looked at.
    /// </summary>
    public const string TransactionsConsidered = "fintrack.transactions.considered";

    /// <summary>
    /// Transactions the work changed.
    /// </summary>
    public const string TransactionsChanged = "fintrack.transactions.changed";

    /// <summary>
    /// Pending transactions the bank stopped reporting, removed as stale.
    /// </summary>
    public const string PendingRemoved = "fintrack.pending.removed";

    /// <summary>
    /// Fetches from the bank that failed in a connection's sync.
    /// </summary>
    public const string FetchesFailed = "fintrack.fetches.failed";

    /// <summary>
    /// The enabled rules that ran.
    /// </summary>
    public const string Rules = "fintrack.rules";

    /// <summary>
    /// The rules that matched at least one transaction.
    /// </summary>
    public const string RulesMatched = "fintrack.rules.matched";

    /// <summary>
    /// Transfers matched.
    /// </summary>
    public const string Transfers = "fintrack.transfers";

    /// <summary>
    /// Possible transfers left for the user to review.
    /// </summary>
    public const string TransfersToReview = "fintrack.transfers.to_review";

    /// <summary>
    /// Transfer answers cleared as no longer matching.
    /// </summary>
    public const string TransfersCleared = "fintrack.transfers.cleared";

    /// <summary>
    /// Bills added from standing orders and direct debits.
    /// </summary>
    public const string BillsAdded = "fintrack.bills.added";

    /// <summary>
    /// Bills ended as their standing order or direct debit went.
    /// </summary>
    public const string BillsEnded = "fintrack.bills.ended";

    /// <summary>
    /// Transactions matched to bills.
    /// </summary>
    public const string BillsMatched = "fintrack.bills.matched";

    /// <summary>
    /// Bills whose price changed.
    /// </summary>
    public const string BillsPricesChanged = "fintrack.bills.prices_changed";

    /// <summary>
    /// Subscriptions suggested from repeated payments.
    /// </summary>
    public const string SubscriptionsSuggested = "fintrack.subscriptions.suggested";

    /// <summary>
    /// Incomes found that look like pay.
    /// </summary>
    public const string PayIncomes = "fintrack.pay.incomes";

    /// <summary>
    /// Pay days known.
    /// </summary>
    public const string PayDays = "fintrack.pay.days";

    /// <summary>
    /// Whether the user has chosen which income is their pay.
    /// </summary>
    public const string PayChosen = "fintrack.pay.chosen";

    /// <summary>
    /// Whether the user has turned pay periods off.
    /// </summary>
    public const string PayOff = "fintrack.pay.off";

    /// <summary>
    /// Balance snapshots updated for the day.
    /// </summary>
    public const string SnapshotsRefreshed = "fintrack.snapshots.refreshed";

    /// <summary>
    /// The day's balance snapshots that are stale.
    /// </summary>
    public const string SnapshotsStale = "fintrack.snapshots.stale";

    /// <summary>
    /// Notifications a step raised, each also an event on its span. Duplicates and kinds turned off everywhere
    /// aren't raised.
    /// </summary>
    public const string NotificationsRaised = "fintrack.notifications.raised";

    /// <summary>
    /// Whether the work succeeded or failed, on metrics.
    /// </summary>
    public const string Outcome = "fintrack.outcome";

    /// <summary>
    /// Whether a synced transaction was added or updated, on metrics.
    /// </summary>
    public const string Change = "fintrack.transactions.change";

    /// <summary>
    /// A bank connection's state, ok, renewal due, lapsed or failing, on metrics.
    /// </summary>
    public const string ConnectionState = "fintrack.connection.state";

    /// <summary>
    /// Where a notification was sent on to, email or webhook, on metrics.
    /// </summary>
    public const string NotificationChannel = "fintrack.notification.channel";

    /// <summary>
    /// The kind of notification raised.
    /// </summary>
    public const string NotificationKind = "fintrack.notification.kind";

    /// <summary>
    /// The notification raised.
    /// </summary>
    public const string NotificationId = "fintrack.notification.id";
}
