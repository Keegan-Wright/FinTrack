using FinanceTracker.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure;

public static partial class LoggerMessages
{
    [LoggerMessage(LogLevel.Error, "Error occured syncing standing orders for provider {providerId} with account {accountId} for user {userId}")]
    public static partial void LogErrorSyncingStandingOrders(this ILogger logger, Exception exception, Guid providerId, string accountId, Guid userId);

    [LoggerMessage(LogLevel.Error, "Error occured syncing direct debits for provider {providerId} with account {accountId} for user {userId}")]
    public static partial void LogErrorSyncingDirectDebits(this ILogger logger, Exception exception, Guid providerId, string accountId, Guid userId);

    [LoggerMessage(LogLevel.Error, "Error occured account balance for provider {providerId} with account {accountId} for user {userId}")]
    public static partial void LogErrorSyncingAccountBalance(this ILogger logger, Exception exception, Guid providerId, string accountId, Guid userId);

    [LoggerMessage(LogLevel.Error, "Error occured syncing transactions for provider {providerId} with account {accountId} for user {userId}")]
    public static partial void LogErrorSyncingTransactions(this ILogger logger, Exception exception, Guid providerId, string accountId, Guid userId);

    [LoggerMessage(LogLevel.Error, "Error occured syncing pending transactions for provider {providerId} with account {accountId} for user {userId}")]
    public static partial void LogErrorSyncingPendingTransactions(this ILogger logger, Exception exception, Guid providerId, string accountId, Guid userId);

    [LoggerMessage(LogLevel.Error, "The browser hit an error for user {userId} on {page}, logged by {category}: {exceptionType}: {errorMessage}\n{stackTrace}")]
    public static partial void LogBrowserError(this ILogger logger, Guid userId, string page, string category, string? exceptionType, string errorMessage, string? stackTrace);

    [LoggerMessage(LogLevel.Information, "Synced connection {connectionId} for user {userId} in {elapsedMs}ms: {accounts} accounts, {cards} cards, {added} transactions added, {updated} updated, {pendingRemoved} stale pending removed, {failedFetches} fetches failed")]
    public static partial void LogSyncedConnection(this ILogger logger, Guid connectionId, Guid userId, int accounts, int cards, int added, int updated, int pendingRemoved, int failedFetches, long elapsedMs);

    [LoggerMessage(LogLevel.Information, "Removed {count} stale pending transactions from account {accountId} for user {userId}")]
    public static partial void LogRemovedStalePendingTransactions(this ILogger logger, int count, Guid accountId, Guid userId);

    [LoggerMessage(LogLevel.Information, "Matched transfers for user {userId} over {considered} transactions: {transfers} transfers, {toReview} to review, {cleared} cleared")]
    public static partial void LogMatchedTransfers(this ILogger logger, Guid userId, int considered, int transfers, int toReview, int cleared);

    [LoggerMessage(LogLevel.Debug, "No transactions changed for user {userId} since transfers were last matched")]
    public static partial void LogNoTransferChanges(this ILogger logger, Guid userId);

    [LoggerMessage(LogLevel.Warning, "TrueLayer refused the {requested} request with {statusCode} {reasonPhrase}, tl-trace-id {traceId}")]
    public static partial void LogTrueLayerRequestRefused(this ILogger logger, string requested, int statusCode, string? reasonPhrase, string traceId);

    [LoggerMessage(LogLevel.Warning, "Connection {connectionId} for user {userId} has no active consent, re-authentication is required before it can sync")]
    public static partial void LogConnectionNotAuthenticated(this ILogger logger, Guid connectionId, Guid userId);

    [LoggerMessage(LogLevel.Information, "Updated {refreshed} balance snapshots for {day} for user {userId}, {stale} of the day's snapshots are stale")]
    public static partial void LogSnapshotBalances(this ILogger logger, Guid userId, DateOnly day, int refreshed, int stale);

    [LoggerMessage(LogLevel.Information, "Worked out {days} past closing balances across {accounts} accounts for user {userId}")]
    public static partial void LogBackfilledBalanceHistory(this ILogger logger, Guid userId, int accounts, int days);

    [LoggerMessage(LogLevel.Debug, "No account has a balance to snapshot for {day} for user {userId}")]
    public static partial void LogNoBalancesToSnapshot(this ILogger logger, Guid userId, DateOnly day);

    [LoggerMessage(LogLevel.Information, "Re-encrypted {count} {entityType} rows with encryption key {keyId}")]
    public static partial void LogReencrypted(this ILogger logger, int count, string entityType, int keyId);

    [LoggerMessage(LogLevel.Information, "Applied {rules} rules for user {userId} to {considered} transactions, {changed} changed")]
    public static partial void LogAppliedRules(this ILogger logger, Guid userId, int rules, int considered, int changed);

    [LoggerMessage(LogLevel.Debug, "Rule {ruleId} matched {matched} transactions for user {userId}")]
    public static partial void LogRuleMatched(this ILogger logger, Guid userId, Guid ruleId, int matched);

    [LoggerMessage(LogLevel.Information, "Re-applied rules to the {considered} transactions rule {ruleId} reached for user {userId}, {changed} changed")]
    public static partial void LogReappliedRules(this ILogger logger, Guid userId, Guid ruleId, int considered, int changed);

    [LoggerMessage(LogLevel.Information, "Applied rule {ruleId} to the {reached} existing transactions it reaches for user {userId}, {changed} changed")]
    public static partial void LogAppliedRuleToExisting(this ILogger logger, Guid userId, Guid ruleId, int reached, int changed);

    [LoggerMessage(LogLevel.Information, "Synced bills for user {userId}, {added} added, {ended} ended, {matched} transactions matched, {pricesChanged} prices changed, {suggested} subscriptions suggested")]
    public static partial void LogSyncedBills(this ILogger logger, Guid userId, int added, int ended, int matched, int pricesChanged, int suggested);

    [LoggerMessage(LogLevel.Information, "Synced pay for user {userId}, {incomes} incomes like pay found, {payDays} pay days known, chosen {chosen}, off {off}")]
    public static partial void LogSyncedPay(this ILogger logger, Guid userId, int incomes, int payDays, bool chosen, bool off);

    [LoggerMessage(LogLevel.Information, "Deleted debt {debtId} for user {userId}, {payments} payments and {rules} rules no longer pay it off")]
    public static partial void LogDeletedDebt(this ILogger logger, Guid userId, Guid debtId, int payments, int rules);

    [LoggerMessage(LogLevel.Information, "Notified user {userId} of {kind} with notification {notificationId}: in app {inApp}, emailed {emailed}, webhook {webhook}")]
    public static partial void LogNotified(this ILogger logger, Guid userId, NotificationKind kind, Guid notificationId, bool inApp, bool emailed, bool webhook);

    [LoggerMessage(LogLevel.Warning, "Connection {connectionId} for user {userId} has started failing to sync, they've been told")]
    public static partial void LogConnectionSyncFailing(this ILogger logger, Guid connectionId, Guid userId);

    [LoggerMessage(LogLevel.Information, "Connection {connectionId} for user {userId} syncs again, it had been failing since {failingSince}")]
    public static partial void LogConnectionSyncRecovered(this ILogger logger, Guid connectionId, Guid userId, DateTime failingSince);

    [LoggerMessage(LogLevel.Warning, "Couldn't email notification {notificationId} to user {userId}, it still shows in the app")]
    public static partial void LogNotificationEmailFailed(this ILogger logger, Exception exception, Guid notificationId, Guid userId);

    [LoggerMessage(LogLevel.Warning, "Couldn't post notification {notificationId} to user {userId}'s webhook, it still shows in the app")]
    public static partial void LogNotificationWebhookFailed(this ILogger logger, Exception exception, Guid notificationId, Guid userId);

    [LoggerMessage(LogLevel.Warning, "Couldn't post user {userId} their test webhook")]
    public static partial void LogTestWebhookFailed(this ILogger logger, Exception exception, Guid userId);

    [LoggerMessage(LogLevel.Warning, "Couldn't send user {userId} their test email")]
    public static partial void LogTestEmailFailed(this ILogger logger, Exception exception, Guid userId);

    [LoggerMessage(LogLevel.Warning, "Couldn't record how notification {notificationId} was sent for user {userId}, their settings were saved by something else at the same moment")]
    public static partial void LogNotificationDeliveryNotRecorded(this ILogger logger, Exception exception, Guid notificationId, Guid userId);
}
