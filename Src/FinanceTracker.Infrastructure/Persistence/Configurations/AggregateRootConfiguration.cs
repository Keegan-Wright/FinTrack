using FinanceTracker.Domain.Identity;
using FinanceTracker.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

/// <summary>
/// Shared mapping for every aggregate root: client generated key, xmin concurrency token and a required,
/// cascading owner.
/// </summary>
internal abstract class AggregateRootConfiguration<T> : IEntityTypeConfiguration<T> where T : AggregateRoot
{
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

    protected abstract void ConfigureAggregate(EntityTypeBuilder<T> builder);
}
