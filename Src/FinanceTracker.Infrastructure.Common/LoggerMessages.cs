using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Common;

/// <summary>
/// Source generated log messages for the shared encryption and row integrity, see
/// <c>FinanceTracker.Infrastructure.LoggerMessages</c> for the core's.
/// </summary>
/// <remarks>
/// The methods take the logger as a <c>this</c> parameter rather than sitting in an <c>extension</c> block, as the
/// logging source generator only recognises the classic form.
/// </remarks>
internal static partial class LoggerMessages
{
    /// <summary>
    /// Logs at information <c>Re-encrypted {count} {entityType} rows with encryption key {keyId}</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="count">How many.</param>
    /// <param name="entityType">The entity's type.</param>
    /// <param name="keyId">The key's id.</param>
    [LoggerMessage(LogLevel.Information, "Re-encrypted {count} {entityType} rows with encryption key {keyId}")]
    public static partial void LogReencrypted(this ILogger logger, int count, string entityType, int keyId);

    /// <summary>
    /// Logs at critical <c>Row {id} in {table} failed its integrity check, it was changed in the database outside
    /// FinTrack</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="table">The row's table.</param>
    /// <param name="id">The row's id.</param>
    [LoggerMessage(LogLevel.Critical,
        "Row {id} in {table} failed its integrity check, it was changed in the database outside FinTrack")]
    public static partial void LogRowIntegrityFailed(this ILogger logger, string table, string id);

    /// <summary>
    /// Logs at warning <c>Row {id} in {table} has no integrity tag yet, so it wasn't checked</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="table">The row's table.</param>
    /// <param name="id">The row's id.</param>
    [LoggerMessage(LogLevel.Warning, "Row {id} in {table} has no integrity tag yet, so it wasn't checked")]
    public static partial void LogRowIntegrityTagMissing(this ILogger logger, string table, string id);

    /// <summary>
    /// Logs at warning <c>Row {id} in {table} has an integrity tag from an older format, so it wasn't checked</c>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="table">The row's table.</param>
    /// <param name="id">The row's id.</param>
    [LoggerMessage(LogLevel.Warning,
        "Row {id} in {table} has an integrity tag from an older format, so it wasn't checked")]
    public static partial void LogRowIntegrityTagOutdated(this ILogger logger, string table, string id);
}
