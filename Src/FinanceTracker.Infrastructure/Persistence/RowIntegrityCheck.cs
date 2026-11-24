using System.Security.Cryptography;
using FinanceTracker.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Persistence;

/// <summary>
/// Checks one loaded row against its integrity tag, see <see cref="RowIntegrity"/>. A row whose tag doesn't match was
/// changed outside the app, so it's refused, logged and counted rather than shown. A row without a tag yet, or with
/// one from an older format, is let through and logged, and tagged at startup.
/// </summary>
internal static class RowIntegrityCheck
{
    /// <summary>
    /// Checks the row, throwing when it doesn't match its tag.
    /// </summary>
    /// <param name="context">The context loading it.</param>
    /// <param name="entityType">Its entity type.</param>
    /// <param name="entity">Its entity, which its complex types' values are read from.</param>
    /// <param name="tag">Its tag, null when it has none.</param>
    /// <param name="valueOf">Each of the row's own covered properties' values as loaded.</param>
    /// <exception cref="RowIntegrityException">The row doesn't match its tag.</exception>
    public static void Verify(FinanceTrackerContext context, IEntityType entityType, object entity, string? tag,
        Func<IProperty, object?> valueOf)
    {
        string table = entityType.GetTableName() ?? entityType.Name;
        if (tag is null)
        {
            LoggerFor(context)?.LogRowIntegrityTagMissing(table, IdOf(entityType, valueOf));
            return;
        }

        if (!RowIntegrity.IsCurrentFormat(tag))
        {
            LoggerFor(context)?.LogRowIntegrityTagOutdated(table, IdOf(entityType, valueOf));
            return;
        }

        byte[] content = RowIntegrity.Content(entityType, valueOf, entity);
        bool valid;
        try
        {
            valid = RowIntegrity.IsValid(context.EncryptionService, tag, content);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(content);
        }

        if (valid)
        {
            return;
        }

        string id = IdOf(entityType, valueOf);
        LoggerFor(context)?.LogRowIntegrityFailed(table, id);
        AppService<FinTrackMetrics>(context)?.RowIntegrityFailed(table);
        throw new RowIntegrityException(table, id);
    }

    /// <summary>
    /// The row's key, its columns' values joined by commas.
    /// </summary>
    /// <param name="entityType">Its entity type.</param>
    /// <param name="valueOf">Each of the row's own properties' values.</param>
    private static string IdOf(IEntityType entityType, Func<IProperty, object?> valueOf) =>
        string.Join(",", entityType.FindPrimaryKey()!.Properties.Select(valueOf));

    /// <summary>
    /// The logger, null for a context made without the app's services, e.g. at design time.
    /// </summary>
    /// <param name="context">The context loading the row.</param>
    private static ILogger? LoggerFor(DbContext context) =>
        AppService<ILoggerFactory>(context)?.CreateLogger(typeof(RowIntegrityCheck));

    /// <summary>
    /// One of the app's services, null for a context made without them.
    /// </summary>
    /// <param name="context">The context loading the row.</param>
    private static T? AppService<T>(DbContext context) where T : class =>
        context.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>()?.ApplicationServiceProvider
            ?.GetService<T>();
}
