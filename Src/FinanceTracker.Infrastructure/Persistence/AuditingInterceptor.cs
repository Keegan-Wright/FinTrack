using FinanceTracker.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

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

    /// <summary>
    /// Whether something changed other than the app's own decisions, e.g. a transaction's transfer decision or rule
    /// tags, see <see cref="AppDecisionBuilderExtensions"/>. The rules and transfer matching runs don't count those as
    /// changes when they look for what changed since their last run, but do count what the bank sends, such as a
    /// merchant name that arrives late.
    /// </summary>
    /// <param name="entry">The entity's entry.</param>
    private static bool ChangedBeyondAppDecisions(EntityEntry entry) =>
        entry.Properties.Any(static x => x.IsModified)
        || entry.ComplexProperties.Any(static x => x.IsModified && !IsAppDecision(x.Metadata))
        || entry.ComplexCollections.Any(static x => x.IsModified && !IsAppDecision(x.Metadata));

    /// <summary>
    /// Whether <paramref name="complexProperty"/> holds a decision the app makes.
    /// </summary>
    /// <param name="complexProperty">The complex property.</param>
    private static bool IsAppDecision(IComplexProperty complexProperty) =>
        complexProperty.FindAnnotation(AppDecisionBuilderExtensions.AppDecisionAnnotation) is not null;

    /// <summary>
    /// Stamps the created and updated times on what's being saved.
    /// </summary>
    /// <param name="context">The context being saved.</param>
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
        DateTime now = clock.UtcNow;
        foreach (EntityEntry<IAuditable> entry in context.ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    // By name, the interface member expression doesn't resolve to the mapped property
                    entry.Property(nameof(IAuditable.Created)).CurrentValue = now;
                    break;
                case EntityState.Modified when ChangedBeyondAppDecisions(entry):
                    entry.Property(nameof(IAuditable.Updated)).CurrentValue = now;
                    break;
            }
        }
    }
}
