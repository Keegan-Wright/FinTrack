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

    [LoggerMessage(LogLevel.Information, "Applied {rules} rules for user {userId}, {changed} transactions changed")]
    public static partial void LogAppliedRules(this ILogger logger, Guid userId, int rules, int changed);

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
