    using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FinanceTracker.Infrastructure.Persistence;

/// <summary>
/// Fills blind index columns from their encrypted properties on save, see <see cref="EntityTypeBuilderExtensions"/>.
/// </summary>
internal sealed class BlindIndexInterceptor : SaveChangesInterceptor
{
    public static BlindIndexInterceptor Instance { get; } = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Compute(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Compute(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void Compute(DbContext? context)
    {
        if (context is not FinanceTrackerContext financeTrackerContext)
        {
            return;
        }

        foreach (EntityEntry entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            foreach (IProperty blindIndex in entry.Metadata.GetProperties())
            {
                if (blindIndex.FindAnnotation(Annotations.BlindIndexSourceAnnotation)?.Value is not string source)
                {
                    continue;
                }

                string value = entry.Property(source).CurrentValue as string ?? string.Empty;
                entry.Property(blindIndex).CurrentValue =
                    financeTrackerContext.EncryptionService.ComputeBlindIndex(value);
            }
        }
    }
}
