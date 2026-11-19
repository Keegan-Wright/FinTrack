namespace FinanceTracker;

/// <summary>
/// Source generated log messages for the web app, see <c>FinanceTracker.Infrastructure.LoggerMessages</c> for the
/// infrastructure equivalents.
/// </summary>
/// <remarks>
/// The methods take the logger as a <c>this</c> parameter rather than sitting in an <c>extension</c> block, as the
/// logging source generator only recognises the classic form.
/// </remarks>
public static partial class WebLoggerMessages
{
    [LoggerMessage(LogLevel.Information, "User logged in")]
    public static partial void LogUserLoggedIn(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "User logged in with a passkey")]
    public static partial void LogUserLoggedInWithPasskey(this ILogger logger);

    [LoggerMessage(LogLevel.Warning, "User account locked out")]
    public static partial void LogUserLockedOut(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "User created a new account with password")]
    public static partial void LogUserRegistered(this ILogger logger);

    [LoggerMessage(LogLevel.Warning, "Failed to notify the browsers of user {userId} of a completed sync")]
    public static partial void LogSyncNotificationFailed(this ILogger logger, Exception exception, Guid userId);

    [LoggerMessage(LogLevel.Warning, "Failed to tell admins' browsers that job {function} started or finished")]
    public static partial void LogJobNotificationFailed(this ILogger logger, Exception exception, string function);

    [LoggerMessage(LogLevel.Information, "Granted the admin role to user {userId}")]
    public static partial void LogAdminGranted(this ILogger logger, Guid userId);


    [LoggerMessage(LogLevel.Warning, "OIDC sign in rejected, {reason}")]
    public static partial void LogOidcSignInRejected(this ILogger logger, string reason);

    [LoggerMessage(LogLevel.Warning, "OIDC sign in failed at the provider")]
    public static partial void LogOidcRemoteFailure(this ILogger logger, Exception? exception);

    [LoggerMessage(LogLevel.Warning, "OIDC sign in couldn't start, the provider refused or couldn't be reached")]
    public static partial void LogOidcChallengeFailed(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Information, "Linked user {userId} to their OIDC subject")]
    public static partial void LogOidcUserLinked(this ILogger logger, Guid userId);

    [LoggerMessage(LogLevel.Information, "User {userId} created from an OIDC sign in")]
    public static partial void LogOidcUserRegistered(this ILogger logger, Guid userId);

    [LoggerMessage(LogLevel.Warning, "Failed to tell the browsers of user {userId} about a new notification")]
    public static partial void LogNotificationPushFailed(this ILogger logger, Exception exception, Guid userId);

    [LoggerMessage(LogLevel.Warning, "An account email for user {userId} wasn't sent, no mail server is set up")]
    public static partial void LogAccountEmailSkipped(this ILogger logger, Guid userId);

    [LoggerMessage(LogLevel.Error, "Failed to send a password reset email to user {userId}")]
    public static partial void LogPasswordResetEmailFailed(this ILogger logger, Exception exception, Guid userId);

    [LoggerMessage(LogLevel.Error, "Failed to send an email confirmation link to user {userId}")]
    public static partial void LogConfirmationEmailFailed(this ILogger logger, Exception exception, Guid userId);

    [LoggerMessage(LogLevel.Information, "User {userId} changed their password")]
    public static partial void LogPasswordChanged(this ILogger logger, Guid userId);

    [LoggerMessage(LogLevel.Information, "User {userId} changed their email")]
    public static partial void LogEmailChanged(this ILogger logger, Guid userId);

    [LoggerMessage(LogLevel.Information, "Admin {adminId} made a password reset link for user {userId}")]
    public static partial void LogResetLinkCreated(this ILogger logger, Guid adminId, Guid userId);

    [LoggerMessage(LogLevel.Information, "Admin {adminId} locked user {userId}")]
    public static partial void LogUserLocked(this ILogger logger, Guid adminId, Guid userId);

    [LoggerMessage(LogLevel.Information, "Admin {adminId} unlocked user {userId}")]
    public static partial void LogUserUnlocked(this ILogger logger, Guid adminId, Guid userId);

    [LoggerMessage(LogLevel.Information,
        "User {userId} is in the identity provider's admins group, made them an admin")]
    public static partial void LogAdminFromGroupGranted(this ILogger logger, Guid userId);

    [LoggerMessage(LogLevel.Information,
        "User {userId} is no longer in the identity provider's admins group, took their admin role")]
    public static partial void LogAdminFromGroupRemoved(this ILogger logger, Guid userId);

    [LoggerMessage(LogLevel.Warning,
        "Nobody is an admin. Someone in the identity provider's admins group becomes one when they next sign in, " +
        "without single sign-on grant the role in the database")]
    public static partial void LogNoAdminLeft(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "User {userId} turned on two-factor sign in")]
    public static partial void LogTwoFactorEnabled(this ILogger logger, Guid userId);

    [LoggerMessage(LogLevel.Information, "User {userId} turned off two-factor sign in")]
    public static partial void LogTwoFactorDisabled(this ILogger logger, Guid userId);

    [LoggerMessage(LogLevel.Information, "User {userId} reset their authenticator key")]
    public static partial void LogAuthenticatorReset(this ILogger logger, Guid userId);

    [LoggerMessage(LogLevel.Information, "User {userId} made new recovery codes")]
    public static partial void LogRecoveryCodesMade(this ILogger logger, Guid userId);

    [LoggerMessage(LogLevel.Warning,
        "Admin {adminId} turned off two-factor sign in for user {userId}, resetting their authenticator and recovery " +
        "codes")]
    public static partial void LogTwoFactorTurnedOffByAdmin(this ILogger logger, Guid adminId, Guid userId);

    [LoggerMessage(LogLevel.Information, "User {userId} added a passkey")]
    public static partial void LogPasskeyAdded(this ILogger logger, Guid userId);

    [LoggerMessage(LogLevel.Information, "User {userId} removed a passkey")]
    public static partial void LogPasskeyRemoved(this ILogger logger, Guid userId);

    [LoggerMessage(LogLevel.Warning, "A passkey for user {userId} couldn't be added: {reason}")]
    public static partial void LogPasskeyNotAdded(this ILogger logger, Guid userId, string reason);

    [LoggerMessage(LogLevel.Warning, "Admin {adminId} removed all {count} passkeys of user {userId}")]
    public static partial void LogPasskeysRemovedByAdmin(this ILogger logger, Guid adminId, int count, Guid userId);

    [LoggerMessage(LogLevel.Warning,
        "The email telling user {userId} how they sign in changed couldn't be sent, the change was still made")]
    public static partial void LogSecurityEmailFailed(this ILogger logger, Exception exception, Guid userId);

    [LoggerMessage(LogLevel.Information, "Admin {adminId} made user {userId} an admin")]
    public static partial void LogAdminRoleGranted(this ILogger logger, Guid adminId, Guid userId);

    [LoggerMessage(LogLevel.Information, "Admin {adminId} took the admin role from user {userId}")]
    public static partial void LogAdminRoleRemoved(this ILogger logger, Guid adminId, Guid userId);
}
