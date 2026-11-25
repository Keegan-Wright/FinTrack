using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FinanceTracker.Infrastructure.Common.Persistence;

/// <summary>
/// Works out the integrity tag of each row being added or changed, see <see cref="RowIntegrity"/>. A legitimate move,
/// e.g. merging a joint account, is saved through the app and so tagged again. Checked on load by
/// <see cref="RowIntegrityCheckInterceptor"/>.
/// </summary>
internal sealed class RowIntegrityInterceptor : SaveChangesInterceptor
{
    public static RowIntegrityInterceptor Instance { get; } = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Tag(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Tag(eventData.Context);
        return ValueTask.FromResult(result);
    }

    /// <summary>
    /// Tags the rows being saved.
    /// </summary>
    /// <param name="context">The context being saved.</param>
    private static void Tag(DbContext? context)
    {
        if (context is not IEncryptedContext encryptedContext)
        {
            return;
        }

        foreach (EntityEntry entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified) || !RowIntegrity.IsTagged(entry.Metadata))
            {
                continue;
            }

            byte[] content = RowIntegrity.Content(entry.Metadata, property => entry.Property(property).CurrentValue,
                entry.Entity);
            try
            {
                entry.Property(RowIntegrity.TagProperty).CurrentValue =
                    RowIntegrity.Tag(encryptedContext.EncryptionService, content);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(content);
            }
        }
    }
}
