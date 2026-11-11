using FinanceTracker.Domain.Sharing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class ShareConfiguration : AggregateRootConfiguration<Share>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<Share> builder)
    {
        builder.ToTable("Shares");

        // Removed with the link's row, as when either user is deleted. Ending a link removes them too, see SharingService
        builder.HasOne<UserLink>()
            .WithMany()
            .HasForeignKey(x => x.LinkId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.ItemType).HasConversion<string>();
        builder.Property(x => x.Mode).HasConversion<string>();

        // Each thing is shared once through a link, and looked up by what's shared when reading it
        builder.HasIndex(x => new { x.LinkId, x.ItemType, x.ItemId }).IsUnique();
        builder.HasIndex(x => new { x.ItemType, x.ItemId });
    }
}
