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

    [LoggerMessage(LogLevel.Information, "Matched transfers for user {userId}: {transfers} transfers, {toReview} to review, {cleared} cleared")]
    public static partial void LogMatchedTransfers(this ILogger logger, Guid userId, int transfers, int toReview, int cleared);

    [LoggerMessage(LogLevel.Warning, "Connection {connectionId} for user {userId} has no active consent, re-authentication is required before it can sync")]
    public static partial void LogConnectionNotAuthenticated(this ILogger logger, Guid connectionId, Guid userId);

    [LoggerMessage(LogLevel.Information, "Re-encrypted {count} {entityType} rows with encryption key {keyId}")]
    public static partial void LogReencrypted(this ILogger logger, int count, string entityType, int keyId);
}
