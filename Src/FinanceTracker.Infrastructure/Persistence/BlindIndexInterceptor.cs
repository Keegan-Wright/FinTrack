    using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FinanceTracker.Infrastructure.Persistence;

/// <summary>
/// Fills blind index columns from their encrypted properties on save, see <see cref="EntityTypeBuilderExtensions"/>.
/// Key rotation marks every encrypted property changed, so they're recomputed under the new key too.
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
                if (ValueToHash(entry, blindIndex) is { } value)
                {
                    entry.Property(blindIndex).CurrentValue =
                        financeTrackerContext.EncryptionService.ComputeBlindIndex(value);
                }
            }
        }
    }

    /// <summary>
    /// What <paramref name="property"/> hashes when it's a blind index, null for any other property.
    /// </summary>
    private static string? ValueToHash(EntityEntry entry, IProperty property)
    {
        // The encrypted value itself
        if (property.FindAnnotation(Annotations.BlindIndexSourceAnnotation)?.Value is string source)
        {
            return entry.Property(source).CurrentValue as string ?? string.Empty;
        }

        // The UTC day of an encrypted date
        if (property.FindAnnotation(Annotations.DayBlindIndexSourceAnnotation)?.Value is string daySource)
        {
            return DayBlindIndex.Value((DateTime)entry.Property(daySource).CurrentValue!);
        }

        return null;
    }
}
