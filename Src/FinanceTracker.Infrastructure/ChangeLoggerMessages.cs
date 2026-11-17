using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure;

/// <summary>
/// What users change through the app, one shape for all of them so the logs can be searched by
/// <c>action</c>, <c>entity</c> and <c>entityId</c>. Ids only, never names or amounts.
/// </summary>
/// <remarks>
/// The methods take the logger as a <c>this</c> parameter rather than sitting in an <c>extension</c> block, as the
/// logging source generator only recognises the classic form.
/// </remarks>
public static partial class ChangeLoggerMessages
{
    /// <summary>
    /// A change to one thing, e.g. "User x created rule y".
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="userId">The user.</param>
    /// <param name="action">What they did, e.g. <c>created</c>.</param>
    /// <param name="entity">What it was, e.g. <c>rule</c>.</param>
    /// <param name="entityId">Its id.</param>
    [LoggerMessage(LogLevel.Information, "User {userId} {action} {entity} {entityId}")]
    public static partial void LogChanged(this ILogger logger, Guid userId, string action, string entity,
        Guid entityId);

    /// <summary>
    /// A change to something of theirs with no single id, e.g. "User x reordered their rules".
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="userId">The user.</param>
    /// <param name="action">What they did, e.g. <c>created</c>.</param>
    /// <param name="entity">What it was, e.g. <c>rule</c>.</param>
    [LoggerMessage(LogLevel.Information, "User {userId} {action} their {entity}")]
    public static partial void LogChangedTheir(this ILogger logger, Guid userId, string action, string entity);

    /// <summary>
    /// Logs at information: <c>User {userId} ran job {job} now</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="userId">The user.</param>
    /// <param name="job">The job.</param>
    [LoggerMessage(LogLevel.Information, "User {userId} ran job {job} now")]
    public static partial void LogJobRunNow(this ILogger logger, Guid userId, string job);

    /// <summary>
    /// Logs at information: <c>User {userId} changed job {job}'s settings: enabled {enabled}, schedule {schedule}</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="userId">The user.</param>
    /// <param name="job">The job.</param>
    /// <param name="enabled">Whether it's enabled.</param>
    /// <param name="schedule">Its cron expression.</param>
    [LoggerMessage(LogLevel.Information,
        "User {userId} changed job {job}'s settings: enabled {enabled}, schedule {schedule}")]
    public static partial void LogJobSettingsChanged(this ILogger logger, Guid userId, string job, bool enabled,
        string schedule);

    /// <summary>
    /// Logs at information: <c>User {userId} exported {export}</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="userId">The user.</param>
    /// <param name="export">What was exported.</param>
    [LoggerMessage(LogLevel.Information, "User {userId} exported {export}")]
    public static partial void LogExported(this ILogger logger, Guid userId, string export);
}
