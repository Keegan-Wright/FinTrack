using FinanceTracker.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Persistence;

/// <summary>
/// Checks each row's integrity tag as it's loaded, see <see cref="RowIntegrity"/>. A row whose tag doesn't match was
/// changed outside the app, so it's refused, logged and counted rather than shown. A row without a tag yet, written
/// before tags or not yet filled in at startup, is let through and logged.
/// </summary>
internal sealed class RowIntegrityCheckInterceptor : IMaterializationInterceptor
{
    public static RowIntegrityCheckInterceptor Instance { get; } = new();

    public object InitializedInstance(MaterializationInterceptionData materializationData, object entity)
    {
        if (!RowIntegrity.IsTagged(materializationData.EntityType) ||
            materializationData.Context is not FinanceTrackerContext context)
        {
            return entity;
        }

        string table = materializationData.EntityType.GetTableName() ?? materializationData.EntityType.Name;
        string id = string.Join(",", materializationData.EntityType.FindPrimaryKey()!.Properties
            .Select(x => materializationData.GetPropertyValue(x)));
        string? tag = materializationData.GetPropertyValue<string?>(RowIntegrity.TagProperty);
        if (tag is null)
        {
            LoggerFor(context)?.LogRowIntegrityTagMissing(table, id);
            return entity;
        }

        byte[] content = RowIntegrity.Content(materializationData.EntityType, materializationData.GetPropertyValue);
        if (context.EncryptionService.IsRowTagValid(tag, content))
        {
            return entity;
        }

        LoggerFor(context)?.LogRowIntegrityFailed(table, id);
        AppService<FinTrackMetrics>(context)?.RowIntegrityFailed(table);
        throw new RowIntegrityException(table, id);
    }

    /// <summary>
    /// The logger, null for a context made without the app's services, e.g. at design time.
    /// </summary>
    /// <param name="context">The context loading the row.</param>
    private static ILogger? LoggerFor(DbContext context) =>
        AppService<ILoggerFactory>(context)?.CreateLogger<RowIntegrityCheckInterceptor>();

    /// <summary>
    /// One of the app's services, null for a context made without them.
    /// </summary>
    /// <param name="context">The context loading the row.</param>
    private static T? AppService<T>(DbContext context) where T : class =>
        context.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>()?.ApplicationServiceProvider
            ?.GetService<T>();
}
