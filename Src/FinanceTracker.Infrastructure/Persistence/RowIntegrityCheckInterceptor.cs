using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FinanceTracker.Infrastructure.Persistence;

/// <summary>
/// Checks each row's integrity tag as it's loaded, see <see cref="RowIntegrityCheck"/>. A row whose tag covers JSON
/// is checked once EF has filled the JSON in, just before the query hands it back, see
/// <see cref="PendingRowIntegrityChecks"/>.
/// </summary>
internal sealed class RowIntegrityCheckInterceptor : IMaterializationInterceptor
{
    public static RowIntegrityCheckInterceptor Instance { get; } = new();

    public object InitializedInstance(MaterializationInterceptionData materializationData, object entity)
    {
        IEntityType entityType = materializationData.EntityType;
        if (!RowIntegrity.IsTagged(entityType) || materializationData.Context is not FinanceTrackerContext context)
        {
            return entity;
        }

        string? tag = materializationData.GetPropertyValue<string?>(RowIntegrity.TagProperty);
        if (!RowIntegrity.CoversJson(entityType))
        {
            RowIntegrityCheck.Verify(context, entityType, entity, tag, materializationData.GetPropertyValue);
            return entity;
        }

        // The values are only readable here, the JSON only once the row has been read
        Dictionary<IProperty, object?> values = RowIntegrity.Covered(entityType)
            .ToDictionary(static x => x, materializationData.GetPropertyValue);
        context.PendingRowIntegrityChecks.Add(new PendingRowIntegrityCheck(entityType, entity, tag, values));
        return entity;
    }
}
