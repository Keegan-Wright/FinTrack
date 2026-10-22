using FinanceTracker.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FinanceTracker.Tests.Unit.Shared;

/// <summary>
/// Swaps the Postgres only parts of the model for SQLite equivalents.
/// </summary>
public sealed class SqliteModelCustomizer : RelationalModelCustomizer
{
    public SqliteModelCustomizer(ModelCustomizerDependencies dependencies) : base(dependencies)
    {
    }

    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        // RowVersion is Postgres' xmin, SQLite has nothing to generate it so it is written like any other column
        foreach (IMutableEntityType? entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(x => typeof(AggregateRoot).IsAssignableFrom(x.ClrType)))
        {
            modelBuilder.Entity(entityType.ClrType)
                .Property(nameof(AggregateRoot.RowVersion))
                .ValueGeneratedNever();
        }
    }
}
