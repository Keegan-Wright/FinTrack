using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace FinanceTracker.Infrastructure.Common.Persistence;

/// <summary>
/// The rows a context has loaded whose tags cover JSON, waiting to be checked. EF fills in JSON only after
/// <see cref="RowIntegrityCheckInterceptor"/> has seen a row, so <see cref="RowIntegrityQueryCompiler"/> checks them
/// before a query hands back each result.
/// </summary>
public sealed class PendingRowIntegrityChecks
{
    private readonly List<PendingRowIntegrityCheck> _pending = [];

    /// <summary>
    /// Adds a row to check.
    /// </summary>
    /// <param name="check">The row.</param>
    internal void Add(PendingRowIntegrityCheck check) => _pending.Add(check);

    /// <summary>
    /// Forgets the rows waiting, for a query that failed before they were filled in.
    /// </summary>
    internal void Clear() => _pending.Clear();

    /// <summary>
    /// Checks the rows waiting. A refused row the context already tracks is let go, so it can't be saved.
    /// </summary>
    /// <param name="context">The context that loaded them.</param>
    /// <exception cref="RowIntegrityException">A row doesn't match its tag.</exception>
    internal void VerifyAll(DbContext context)
    {
        if (_pending.Count == 0)
        {
            return;
        }

        PendingRowIntegrityCheck[] checks = [.. _pending];
        _pending.Clear();
        foreach (PendingRowIntegrityCheck check in checks)
        {
            try
            {
                RowIntegrityCheck.Verify(context, check.EntityType, check.Entity, check.Tag,
                    property => check.Values[property]);
            }
            catch (RowIntegrityException)
            {
                StopTracking(context, check.Entity);
                throw;
            }
        }
    }

    /// <summary>
    /// Stops the context tracking <paramref name="entity"/>, when it does.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="entity">The entity.</param>
    private static void StopTracking(DbContext context, object entity)
    {
        EntityEntry? entry = context.ChangeTracker.Entries().FirstOrDefault(x => ReferenceEquals(x.Entity, entity));
        if (entry is not null)
        {
            entry.State = EntityState.Detached;
        }
    }
}
