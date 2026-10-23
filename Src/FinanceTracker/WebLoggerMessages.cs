namespace FinanceTracker;

/// <summary>
/// Source generated log messages for the web app, see <c>FinanceTracker.Infrastructure.LoggerMessages</c> for the
/// infrastructure equivalents.
/// </summary>
public static partial class WebLoggerMessages
{
    [LoggerMessage(LogLevel.Information, "User logged in")]
    public static partial void LogUserLoggedIn(this ILogger logger);

    [LoggerMessage(LogLevel.Warning, "User account locked out")]
    public static partial void LogUserLockedOut(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "User created a new account with password")]
    public static partial void LogUserRegistered(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "User with ID {userId} asked for their personal data")]
    public static partial void LogPersonalDataRequested(this ILogger logger, string userId);

    [LoggerMessage(LogLevel.Warning, "Failed to notify the browsers of user {userId} of a completed sync")]
    public static partial void LogSyncNotificationFailed(this ILogger logger, Exception exception, Guid userId);

    [LoggerMessage(LogLevel.Information, "Granted the admin role to user {userId}")]
    public static partial void LogAdminGranted(this ILogger logger, Guid userId);

    [LoggerMessage(LogLevel.Warning, "Nobody is an admin and there are several users, grant the admin role in the database")]
    public static partial void LogNoAdminAmongSeveralUsers(this ILogger logger);

    [LoggerMessage(LogLevel.Warning, "OIDC sign in rejected, {reason}")]
    public static partial void LogOidcSignInRejected(this ILogger logger, string reason);

    [LoggerMessage(LogLevel.Warning, "OIDC sign in failed at the provider")]
    public static partial void LogOidcRemoteFailure(this ILogger logger, Exception? exception);

    [LoggerMessage(LogLevel.Information, "Linked user {userId} to their OIDC subject")]
    public static partial void LogOidcUserLinked(this ILogger logger, Guid userId);

    [LoggerMessage(LogLevel.Information, "User {userId} created from an OIDC sign in")]
    public static partial void LogOidcUserRegistered(this ILogger logger, Guid userId);
}
