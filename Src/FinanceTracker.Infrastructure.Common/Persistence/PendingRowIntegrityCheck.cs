using Microsoft.EntityFrameworkCore.Metadata;

namespace FinanceTracker.Infrastructure.Common.Persistence;

/// <summary>
/// A loaded row whose check waits for EF to fill in its JSON, see <see cref="PendingRowIntegrityChecks"/>.
/// </summary>
/// <param name="EntityType">Its entity type.</param>
/// <param name="Entity">Its entity.</param>
/// <param name="Tag">Its tag, null when it has none.</param>
/// <param name="Values">Its own covered properties' values as loaded, see <see cref="RowIntegrity.Covered"/>.</param>
internal sealed record PendingRowIntegrityCheck(
    IEntityType EntityType,
    object Entity,
    string? Tag,
    IReadOnlyDictionary<IProperty, object?> Values);
