using FinanceTracker.Domain.Identity;
using FinanceTracker.Domain.Sharing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class UserLinkConfiguration : AggregateRootConfiguration<UserLink>
{
    /// <summary>
    /// The index keeping two people to one link, which saving a second breaks.
    /// </summary>
    public const string OneLinkPerPairIndex = "IX_UserLinks_OneLinkPerPair";

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

        // One link between two people, whoever invited whom, see UserLink.PairOf. Two inviting each other at the same
        // moment can't both be saved
        builder.HasIndex(x => new { x.FirstOfPair, x.SecondOfPair })
            .IsUnique()
            .HasDatabaseName(OneLinkPerPairIndex);
    }
}
