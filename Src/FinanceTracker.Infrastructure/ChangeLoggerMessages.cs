using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure;

/// <summary>
/// What users change through the app, one shape for all of them so the logs can be searched by
/// <c>action</c>, <c>entity</c> and <c>entityId</c>. Ids only, never names or amounts.
/// </summary>
public static partial class ChangeLoggerMessages
{
    /// <summary>
    /// A change to one thing, e.g. "User x created rule y".
    /// </summary>
    [LoggerMessage(LogLevel.Information, "User {userId} {action} {entity} {entityId}")]
    public static partial void LogChanged(this ILogger logger, Guid userId, string action, string entity,
        Guid entityId);

    /// <summary>
    /// A change to something of theirs with no single id, e.g. "User x reordered their rules".
    /// </summary>
    [LoggerMessage(LogLevel.Information, "User {userId} {action} their {entity}")]
    public static partial void LogChangedTheir(this ILogger logger, Guid userId, string action, string entity);

    [LoggerMessage(LogLevel.Information, "User {userId} ran job {job} now")]
    public static partial void LogJobRunNow(this ILogger logger, Guid userId, string job);

    [LoggerMessage(LogLevel.Information, "User {userId} changed job {job}'s settings: enabled {enabled}, schedule {schedule}")]
    public static partial void LogJobSettingsChanged(this ILogger logger, Guid userId, string job, bool enabled,
        string schedule);

    [LoggerMessage(LogLevel.Information, "User {userId} exported {export}")]
    public static partial void LogExported(this ILogger logger, Guid userId, string export);
}
