using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

/// <summary>
/// Shared mapping for every aggregate root: client generated key, xmin concurrency token and a required,
/// cascading owner.
/// </summary>
/// <typeparam name="T">The aggregate.</typeparam>
internal abstract class AggregateRootConfiguration<T> : IEntityTypeConfiguration<T> where T : AggregateRoot
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<T> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne<FinanceTrackerUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        ConfigureAggregate(builder);
    }

    /// <summary>
    /// Maps what's particular to the aggregate.
    /// </summary>
    /// <param name="builder">The aggregate's builder.</param>
    protected abstract void ConfigureAggregate(EntityTypeBuilder<T> builder);
}
