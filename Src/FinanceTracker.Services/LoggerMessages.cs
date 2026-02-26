// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace FinanceTracker.Services;

public static partial class LoggerMessages
{
    [LoggerMessage(LogLevel.Error, "Error occured syncing standing orders for provider {providerId} with account {accountId} for user {userId}")]
    public static partial void LogErrorSyncingStandingOrders(this ILogger logger, Guid providerId, string accountId, Guid userId);

    [LoggerMessage(LogLevel.Error, "Error occured syncing direct debits for provider {providerId} with account {accountId} for user {userId}")]
    public static partial void LogErrorSyncingDirectDebits(this ILogger logger, Guid providerId, string accountId, Guid userId);

    [LoggerMessage(LogLevel.Error, "Error occured account balance for provider {providerId} with account {accountId} for user {userId}")]
    public static partial void LogErrorSyncingAccountBalance(this ILogger logger, Guid providerId, string accountId, Guid userId);

    [LoggerMessage(LogLevel.Error, "Error occured syncing transactions for provider {providerId} with account {accountId} for user {userId}")]
    public static partial void LogErrorSyncingTransactions(this ILogger logger, Guid providerId, string accountId, Guid userId);

    [LoggerMessage(LogLevel.Error, "Error occured syncing pending transactions for provider {providerId} with account {accountId} for user {userId}")]
    public static partial void LogErrorSyncingPendingTransactions(this ILogger logger, Guid providerId, string accountId, Guid userId);

}
