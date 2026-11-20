using FinanceTracker.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure;

/// <summary>
/// Source generated log messages for the infrastructure, see <c>FinanceTracker.WebLoggerMessages</c> for the web
/// app's and <see cref="ChangeLoggerMessages"/> for what users change.
/// </summary>
/// <remarks>
/// The methods take the logger as a <c>this</c> parameter rather than sitting in an <c>extension</c> block, as the
/// logging source generator only recognises the classic form.
/// </remarks>
public static partial class LoggerMessages
{
    /// <summary>
    /// Logs at error: <c>Error occurred syncing standing orders for provider {providerId} with account {accountId} for
    /// user {userId}</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">What went wrong.</param>
    /// <param name="providerId">The bank connection.</param>
    /// <param name="accountId">The account.</param>
    /// <param name="userId">The user.</param>
    [LoggerMessage(LogLevel.Error,
        "Error occurred syncing standing orders for provider {providerId} with account {accountId} for user {userId}")]
    public static partial void LogErrorSyncingStandingOrders(this ILogger logger, Exception exception, Guid providerId,
        string accountId, Guid userId);

    /// <summary>
    /// Logs at error: <c>Error occurred syncing direct debits for provider {providerId} with account {accountId} for
    /// user {userId}</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">What went wrong.</param>
    /// <param name="providerId">The bank connection.</param>
    /// <param name="accountId">The account.</param>
    /// <param name="userId">The user.</param>
    [LoggerMessage(LogLevel.Error,
        "Error occurred syncing direct debits for provider {providerId} with account {accountId} for user {userId}")]
    public static partial void LogErrorSyncingDirectDebits(this ILogger logger, Exception exception, Guid providerId,
        string accountId, Guid userId);

    /// <summary>
    /// Logs at error: <c>Error occurred syncing the account balance for provider {providerId} with account {accountId}
    /// for user {userId}</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">What went wrong.</param>
    /// <param name="providerId">The bank connection.</param>
    /// <param name="accountId">The account.</param>
    /// <param name="userId">The user.</param>
    [LoggerMessage(LogLevel.Error,
        "Error occurred syncing the account balance for provider {providerId} with account {accountId} for user " +
        "{userId}")]
    public static partial void LogErrorSyncingAccountBalance(this ILogger logger, Exception exception, Guid providerId,
        string accountId, Guid userId);

    /// <summary>
    /// Logs at error: <c>Error occurred syncing transactions for provider {providerId} with account {accountId} for
    /// user {userId}</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">What went wrong.</param>
    /// <param name="providerId">The bank connection.</param>
    /// <param name="accountId">The account.</param>
    /// <param name="userId">The user.</param>
    [LoggerMessage(LogLevel.Error,
        "Error occurred syncing transactions for provider {providerId} with account {accountId} for user {userId}")]
    public static partial void LogErrorSyncingTransactions(this ILogger logger, Exception exception, Guid providerId,
        string accountId, Guid userId);

    /// <summary>
    /// Logs at error: <c>Error occurred syncing pending transactions for provider {providerId} with account {accountId}
    /// for user {userId}</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">What went wrong.</param>
    /// <param name="providerId">The bank connection.</param>
    /// <param name="accountId">The account.</param>
    /// <param name="userId">The user.</param>
    [LoggerMessage(LogLevel.Error,
        "Error occurred syncing pending transactions for provider {providerId} with account {accountId} for user " +
        "{userId}")]
    public static partial void LogErrorSyncingPendingTransactions(this ILogger logger, Exception exception,
        Guid providerId, string accountId, Guid userId);

    /// <summary>
    /// Logs at error: <c>The browser hit an error for user {userId} on {page}, logged by {category}: {exceptionType}:
    /// {errorMessage}\n{stackTrace}</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="userId">The user.</param>
    /// <param name="page">The page it happened on.</param>
    /// <param name="category">The logger's category.</param>
    /// <param name="exceptionType">The exception's type.</param>
    /// <param name="errorMessage">The error's message.</param>
    /// <param name="stackTrace">The stack trace.</param>
    [LoggerMessage(LogLevel.Error,
        "The browser hit an error for user {userId} on {page}, logged by {category}: {exceptionType}: " +
        "{errorMessage}\n{stackTrace}")]
    public static partial void LogBrowserError(this ILogger logger, Guid userId, string page, string category,
        string? exceptionType, string errorMessage, string? stackTrace);

    /// <summary>
    /// Logs at information: <c>Synced connection {connectionId} for user {userId} in {elapsedMs}ms: {accounts}
    /// accounts, {cards} cards, {added} transactions added, {updated} updated, {pendingRemoved} stale pending removed,
    /// {failedFetches} fetches failed</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="connectionId">The bank connection.</param>
    /// <param name="userId">The user.</param>
    /// <param name="accounts">How many accounts.</param>
    /// <param name="cards">How many cards.</param>
    /// <param name="added">How many were added.</param>
    /// <param name="updated">How many were updated.</param>
    /// <param name="pendingRemoved">How many pending transactions were removed.</param>
    /// <param name="failedFetches">How many fetches failed.</param>
    /// <param name="elapsedMs">How long it took, in milliseconds.</param>
    [LoggerMessage(LogLevel.Information,
        "Synced connection {connectionId} for user {userId} in {elapsedMs}ms: {accounts} accounts, {cards} cards, " +
        "{added} transactions added, {updated} updated, {pendingRemoved} stale pending removed, {failedFetches} " +
        "fetches failed")]
    public static partial void LogSyncedConnection(this ILogger logger, Guid connectionId, Guid userId, int accounts,
        int cards, int added, int updated, int pendingRemoved, int failedFetches, long elapsedMs);

    /// <summary>
    /// Logs at information: <c>Removed {count} stale pending transactions from account {accountId} for user
    /// {userId}</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="count">How many.</param>
    /// <param name="accountId">The account.</param>
    /// <param name="userId">The user.</param>
    [LoggerMessage(LogLevel.Information,
        "Removed {count} stale pending transactions from account {accountId} for user {userId}")]
    public static partial void LogRemovedStalePendingTransactions(this ILogger logger, int count, Guid accountId,
        Guid userId);

    /// <summary>
    /// Logs at information: <c>Matched transfers for user {userId} over {considered} transactions: {transfers}
    /// transfers, {toReview} to review, {cleared} cleared</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="userId">The user.</param>
    /// <param name="considered">How many were looked at.</param>
    /// <param name="transfers">How many were decided to be transfers.</param>
    /// <param name="toReview">How many were put up for review.</param>
    /// <param name="cleared">How many were cleared of an earlier decision.</param>
    [LoggerMessage(LogLevel.Information,
        "Matched transfers for user {userId} over {considered} transactions: {transfers} transfers, {toReview} to " +
        "review, {cleared} cleared")]
    public static partial void LogMatchedTransfers(this ILogger logger, Guid userId, int considered, int transfers,
        int toReview, int cleared);

    /// <summary>
    /// Logs at debug: <c>No transactions changed for user {userId} since transfers were last matched</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="userId">The user.</param>
    [LoggerMessage(LogLevel.Debug, "No transactions changed for user {userId} since transfers were last matched")]
    public static partial void LogNoTransferChanges(this ILogger logger, Guid userId);

    /// <summary>
    /// Logs at warning: <c>TrueLayer refused the {requested} request with {statusCode} {reasonPhrase}, tl-trace-id
    /// {traceId}</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="requested">What was asked for.</param>
    /// <param name="statusCode">The status the provider answered with.</param>
    /// <param name="reasonPhrase">The provider's reason, null when it gave none.</param>
    /// <param name="traceId">The provider's trace id.</param>
    [LoggerMessage(LogLevel.Warning,
        "TrueLayer refused the {requested} request with {statusCode} {reasonPhrase}, tl-trace-id {traceId}")]
    public static partial void LogTrueLayerRequestRefused(this ILogger logger, string requested, int statusCode,
        string? reasonPhrase, string traceId);

    /// <summary>
    /// Logs at warning: <c>Connection {connectionId} for user {userId} has no active consent, re-authentication is
    /// required before it can sync</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="connectionId">The bank connection.</param>
    /// <param name="userId">The user.</param>
    [LoggerMessage(LogLevel.Warning,
        "Connection {connectionId} for user {userId} has no active consent, re-authentication is required before it " +
        "can sync")]
    public static partial void LogConnectionNotAuthenticated(this ILogger logger, Guid connectionId, Guid userId);

    /// <summary>
    /// Logs at debug: <c>Left out standing orders and direct debits for connection {connectionId} for user {userId},
    /// the bank only releases them just after the user authenticates, last at {consentGrantedAt}. The stored ones are
    /// kept until the next renewal</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="connectionId">The bank connection.</param>
    /// <param name="userId">The user.</param>
    /// <param name="consentGrantedAt">When the user last consented.</param>
    [LoggerMessage(LogLevel.Debug,
        "Left out standing orders and direct debits for connection {connectionId} for user {userId}, the bank only " +
        "releases them just after the user authenticates, last at {consentGrantedAt}. The stored ones are kept until " +
        "the next renewal")]
    public static partial void LogStandingOrdersAndDirectDebitsSkipped(this ILogger logger, Guid connectionId,
        Guid userId, DateTime consentGrantedAt);

    /// <summary>
    /// Logs at information: <c>Updated {refreshed} balance snapshots for {day} for user {userId}, {stale} of the day's
    /// snapshots are stale</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="userId">The user.</param>
    /// <param name="day">The day.</param>
    /// <param name="refreshed">How many were refreshed.</param>
    /// <param name="stale">How many are stale.</param>
    [LoggerMessage(LogLevel.Information,
        "Updated {refreshed} balance snapshots for {day} for user {userId}, {stale} of the day's snapshots are stale")]
    public static partial void LogSnapshotBalances(this ILogger logger, Guid userId, DateOnly day, int refreshed,
        int stale);

    /// <summary>
    /// Logs at information: <c>Worked out {days} past closing balances across {accounts} accounts for user
    /// {userId}</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="userId">The user.</param>
    /// <param name="accounts">How many accounts.</param>
    /// <param name="days">How many days.</param>
    [LoggerMessage(LogLevel.Information,
        "Worked out {days} past closing balances across {accounts} accounts for user {userId}")]
    public static partial void LogBackfilledBalanceHistory(this ILogger logger, Guid userId, int accounts, int days);

    /// <summary>
    /// Logs at debug: <c>No account has a balance to snapshot for {day} for user {userId}</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="userId">The user.</param>
    /// <param name="day">The day.</param>
    [LoggerMessage(LogLevel.Debug, "No account has a balance to snapshot for {day} for user {userId}")]
    public static partial void LogNoBalancesToSnapshot(this ILogger logger, Guid userId, DateOnly day);

    /// <summary>
    /// Logs at information: <c>Re-encrypted {count} {entityType} rows with encryption key {keyId}</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="count">How many.</param>
    /// <param name="entityType">The entity's type.</param>
    /// <param name="keyId">The key's id.</param>
    [LoggerMessage(LogLevel.Information, "Re-encrypted {count} {entityType} rows with encryption key {keyId}")]
    public static partial void LogReencrypted(this ILogger logger, int count, string entityType, int keyId);

    /// <summary>
    /// Logs at information: <c>Applied {rules} rules for user {userId} to {considered} transactions, {changed}
    /// changed</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="userId">The user.</param>
    /// <param name="rules">How many rules.</param>
    /// <param name="considered">How many were looked at.</param>
    /// <param name="changed">How many changed.</param>
    [LoggerMessage(LogLevel.Information,
        "Applied {rules} rules for user {userId} to {considered} transactions, {changed} changed")]
    public static partial void LogAppliedRules(this ILogger logger, Guid userId, int rules, int considered,
        int changed);

    /// <summary>
    /// Logs at debug: <c>Rule {ruleId} matched {matched} transactions for user {userId}</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="userId">The user.</param>
    /// <param name="ruleId">The rule.</param>
    /// <param name="matched">How many matched.</param>
    [LoggerMessage(LogLevel.Debug, "Rule {ruleId} matched {matched} transactions for user {userId}")]
    public static partial void LogRuleMatched(this ILogger logger, Guid userId, Guid ruleId, int matched);

    /// <summary>
    /// Logs at information: <c>Re-applied rules to the {considered} transactions rule {ruleId} reached for user
    /// {userId}, {changed} changed</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="userId">The user.</param>
    /// <param name="ruleId">The rule.</param>
    /// <param name="considered">How many were looked at.</param>
    /// <param name="changed">How many changed.</param>
    [LoggerMessage(LogLevel.Information,
        "Re-applied rules to the {considered} transactions rule {ruleId} reached for user {userId}, {changed} changed")]
    public static partial void LogReappliedRules(this ILogger logger, Guid userId, Guid ruleId, int considered,
        int changed);

    /// <summary>
    /// Logs at information: <c>Applied rule {ruleId} to the {reached} existing transactions it reaches for user
    /// {userId}, {changed} changed</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="userId">The user.</param>
    /// <param name="ruleId">The rule.</param>
    /// <param name="reached">How many the rules reached.</param>
    /// <param name="changed">How many changed.</param>
    [LoggerMessage(LogLevel.Information,
        "Applied rule {ruleId} to the {reached} existing transactions it reaches for user {userId}, {changed} changed")]
    public static partial void LogAppliedRuleToExisting(this ILogger logger, Guid userId, Guid ruleId, int reached,
        int changed);

    /// <summary>
    /// Logs at information: <c>Synced bills for user {userId}, {added} added, {ended} ended, {matched} transactions
    /// matched, {pricesChanged} prices changed, {suggested} subscriptions suggested</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="userId">The user.</param>
    /// <param name="added">How many were added.</param>
    /// <param name="ended">How many ended.</param>
    /// <param name="matched">How many matched.</param>
    /// <param name="pricesChanged">How many changed price.</param>
    /// <param name="suggested">How many were suggested.</param>
    [LoggerMessage(LogLevel.Information,
        "Synced bills for user {userId}, {added} added, {ended} ended, {matched} transactions matched, " +
        "{pricesChanged} prices changed, {suggested} subscriptions suggested")]
    public static partial void LogSyncedBills(this ILogger logger, Guid userId, int added, int ended, int matched,
        int pricesChanged, int suggested);

    /// <summary>
    /// Logs at information: <c>Synced pay for user {userId}, {incomes} incomes like pay found, {payDays} pay days
    /// known, chosen {chosen}, off {off}</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="userId">The user.</param>
    /// <param name="incomes">How many incomes were found.</param>
    /// <param name="payDays">How many pay days.</param>
    /// <param name="chosen">Whether the user chose the pay.</param>
    /// <param name="off">Whether pay periods are off.</param>
    [LoggerMessage(LogLevel.Information,
        "Synced pay for user {userId}, {incomes} incomes like pay found, {payDays} pay days known, chosen {chosen}, " +
        "off {off}")]
    public static partial void LogSyncedPay(this ILogger logger, Guid userId, int incomes, int payDays, bool chosen,
        bool off);

    /// <summary>
    /// Logs at information: <c>Deleted debt {debtId} for user {userId}, {payments} payments and {rules} rules no longer
    /// pay it off</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="userId">The user.</param>
    /// <param name="debtId">The debt.</param>
    /// <param name="payments">How many payments.</param>
    /// <param name="rules">How many rules.</param>
    [LoggerMessage(LogLevel.Information,
        "Deleted debt {debtId} for user {userId}, {payments} payments and {rules} rules no longer pay it off")]
    public static partial void LogDeletedDebt(this ILogger logger, Guid userId, Guid debtId, int payments, int rules);

    /// <summary>
    /// Logs at information: <c>Notified user {userId} of {kind} with notification {notificationId}: in app {inApp},
    /// emailed {emailed}, webhook {webhook}</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="userId">The user.</param>
    /// <param name="kind">What it's about.</param>
    /// <param name="notificationId">The notification.</param>
    /// <param name="inApp">Whether it shows in the app.</param>
    /// <param name="emailed">Whether it's emailed.</param>
    /// <param name="webhook">Whether it's posted to the webhook.</param>
    [LoggerMessage(LogLevel.Information,
        "Notified user {userId} of {kind} with notification {notificationId}: in app {inApp}, emailed {emailed}, " +
        "webhook {webhook}")]
    public static partial void LogNotified(this ILogger logger, Guid userId, NotificationKind kind, Guid notificationId,
        bool inApp, bool emailed, bool webhook);

    /// <summary>
    /// Logs at warning: <c>Connection {connectionId} for user {userId} has started failing to sync, they've been
    /// told</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="connectionId">The bank connection.</param>
    /// <param name="userId">The user.</param>
    [LoggerMessage(LogLevel.Warning,
        "Connection {connectionId} for user {userId} has started failing to sync, they've been told")]
    public static partial void LogConnectionSyncFailing(this ILogger logger, Guid connectionId, Guid userId);

    /// <summary>
    /// Logs at information: <c>Connection {connectionId} for user {userId} syncs again, it had been failing since
    /// {failingSince}</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="connectionId">The bank connection.</param>
    /// <param name="userId">The user.</param>
    /// <param name="failingSince">When it started failing.</param>
    [LoggerMessage(LogLevel.Information,
        "Connection {connectionId} for user {userId} syncs again, it had been failing since {failingSince}")]
    public static partial void LogConnectionSyncRecovered(this ILogger logger, Guid connectionId, Guid userId,
        DateTime failingSince);

    /// <summary>
    /// Logs at warning: <c>Couldn't email notification {notificationId} to user {userId}, it still shows in the
    /// app</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">What went wrong.</param>
    /// <param name="notificationId">The notification.</param>
    /// <param name="userId">The user.</param>
    [LoggerMessage(LogLevel.Warning,
        "Couldn't email notification {notificationId} to user {userId}, it still shows in the app")]
    public static partial void LogNotificationEmailFailed(this ILogger logger, Exception exception, Guid notificationId,
        Guid userId);

    /// <summary>
    /// Logs at warning: <c>Couldn't post notification {notificationId} to user {userId}'s webhook, it still shows in
    /// the app</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">What went wrong.</param>
    /// <param name="notificationId">The notification.</param>
    /// <param name="userId">The user.</param>
    [LoggerMessage(LogLevel.Warning,
        "Couldn't post notification {notificationId} to user {userId}'s webhook, it still shows in the app")]
    public static partial void LogNotificationWebhookFailed(this ILogger logger, Exception exception,
        Guid notificationId, Guid userId);

    /// <summary>
    /// Logs at warning: <c>Couldn't post user {userId} their test webhook</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">What went wrong.</param>
    /// <param name="userId">The user.</param>
    [LoggerMessage(LogLevel.Warning, "Couldn't post user {userId} their test webhook")]
    public static partial void LogTestWebhookFailed(this ILogger logger, Exception exception, Guid userId);

    /// <summary>
    /// Logs at warning: <c>Couldn't send user {userId} their test email</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">What went wrong.</param>
    /// <param name="userId">The user.</param>
    [LoggerMessage(LogLevel.Warning, "Couldn't send user {userId} their test email")]
    public static partial void LogTestEmailFailed(this ILogger logger, Exception exception, Guid userId);

    /// <summary>
    /// Logs at warning: <c>Couldn't record how notification {notificationId} was sent for user {userId}, their settings
    /// were saved by something else at the same moment</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">What went wrong.</param>
    /// <param name="notificationId">The notification.</param>
    /// <param name="userId">The user.</param>
    [LoggerMessage(LogLevel.Warning,
        "Couldn't record how notification {notificationId} was sent for user {userId}, their settings were saved by " +
        "something else at the same moment")]
    public static partial void LogNotificationDeliveryNotRecorded(this ILogger logger, Exception exception,
        Guid notificationId, Guid userId);

    /// <summary>
    /// Logs at warning <c>Couldn't push user {userId} their test to device {deviceId}</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">What went wrong.</param>
    /// <param name="userId">The user.</param>
    /// <param name="deviceId">The device.</param>
    [LoggerMessage(LogLevel.Warning, "Couldn't push user {userId} their test to device {deviceId}")]
    public static partial void LogTestPushFailed(this ILogger logger, Exception exception, Guid userId,
        Guid deviceId);

    /// <summary>
    /// Logs at information <c>Removed user {userId}'s push device {deviceId}, its browser has unsubscribed</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="userId">The user.</param>
    /// <param name="deviceId">The device.</param>
    [LoggerMessage(LogLevel.Information,
        "Removed user {userId}'s push device {deviceId}, its browser has unsubscribed")]
    public static partial void LogPushDeviceGone(this ILogger logger, Guid userId, Guid deviceId);

    /// <summary>
    /// Logs at information <c>Moved push device {deviceId} from user {previousUserId} to user {userId}, the browser
    /// changed hands</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="deviceId">The device the last user had.</param>
    /// <param name="previousUserId">Who had it.</param>
    /// <param name="userId">Who has it now.</param>
    [LoggerMessage(LogLevel.Information,
        "Moved push device {deviceId} from user {previousUserId} to user {userId}, the browser changed hands")]
    public static partial void LogPushDeviceChangedHands(this ILogger logger, Guid deviceId, Guid previousUserId,
        Guid userId);
}
