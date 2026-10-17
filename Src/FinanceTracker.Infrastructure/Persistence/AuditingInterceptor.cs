using FinanceTracker.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FinanceTracker.Infrastructure.Persistence;

/// <summary>
/// Stamps <see cref="IAuditable.Created"/> and <see cref="IAuditable.Updated"/> on save.
/// </summary>
internal sealed class AuditingInterceptor : SaveChangesInterceptor
{
    public static AuditingInterceptor Instance { get; } = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        DateTime now = DateTime.UtcNow;
        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    // By name, the interface member expression doesn't resolve to the mapped property
                    entry.Property(nameof(IAuditable.Created)).CurrentValue = now;
                    break;
                case EntityState.Modified:
                    entry.Property(nameof(IAuditable.Updated)).CurrentValue = now;
                    break;
            }
        }
    }
}
