using FinanceTracker.Domain.Identity;
using FinanceTracker.Domain.Sharing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class UserLinkConfiguration : AggregateRootConfiguration<UserLink>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<UserLink> builder)
    {
        builder.ToTable("UserLinks");

        // Owned by the inviter like any aggregate, and removed with either of them
        builder.HasOne<FinanceTrackerUser>()
            .WithMany()
            .HasForeignKey(x => x.InviteeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.Status).HasConversion<string>();

        // Looked up from either side
        builder.HasIndex(x => x.InviteeId);
    }
}
