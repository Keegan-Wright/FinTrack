using FinanceTracker.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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

        // The interceptor is shared, so the clock comes from the context being saved
        TimeProvider clock = context is FinanceTrackerContext financeTrackerContext
            ? financeTrackerContext.TimeProvider
            : TimeProvider.System;
        DateTime now = clock.GetUtcNow().UtcDateTime;
        foreach (EntityEntry<IAuditable> entry in context.ChangeTracker.Entries<IAuditable>())
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
