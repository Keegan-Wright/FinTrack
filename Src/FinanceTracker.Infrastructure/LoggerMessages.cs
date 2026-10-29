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

    [LoggerMessage(LogLevel.Debug, "No account has a balance to snapshot for {day} for user {userId}")]
    public static partial void LogNoBalancesToSnapshot(this ILogger logger, Guid userId, DateOnly day);

    [LoggerMessage(LogLevel.Information, "Re-encrypted {count} {entityType} rows with encryption key {keyId}")]
    public static partial void LogReencrypted(this ILogger logger, int count, string entityType, int keyId);

    [LoggerMessage(LogLevel.Information, "Applied {rules} rules for user {userId}, {changed} transactions changed")]
    public static partial void LogAppliedRules(this ILogger logger, Guid userId, int rules, int changed);

    [LoggerMessage(LogLevel.Information, "Re-applied rules to the {considered} transactions rule {ruleId} reached for user {userId}, {changed} changed")]
    public static partial void LogReappliedRules(this ILogger logger, Guid userId, Guid ruleId, int considered, int changed);
}
