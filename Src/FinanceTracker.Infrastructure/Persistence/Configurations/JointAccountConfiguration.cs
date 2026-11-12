using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.Sharing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class JointAccountConfiguration : AggregateRootConfiguration<JointAccount>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<JointAccount> builder)
    {
        builder.ToTable("JointAccounts");

        // Gone with the link's row, as when either user is deleted. Ending a link splits them first, see
        // SharingService, so the hidden copy gets the annotations back
        builder.HasOne<UserLink>()
            .WithMany()
            .HasForeignKey(x => x.LinkId)
            .OnDelete(DeleteBehavior.Cascade);

        // Either copy going, its connection removed, ends the merge and the other stands alone
        builder.HasOne<BankAccount>()
            .WithMany()
            .HasForeignKey(x => x.ShownAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<BankAccount>()
            .WithMany()
            .HasForeignKey(x => x.HiddenAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        // The times are the shown copy's transactions', encrypted as those are
        builder.OwnsMany(x => x.Gaps, gaps =>
        {
            gaps.ToJson("Gaps");
            gaps.Property(x => x.From).Encrypted();
            gaps.Property(x => x.To).Encrypted();
        });
        builder.Navigation(x => x.Gaps).HasField("_gaps");

        // Each account is in one merge at most
        builder.HasIndex(x => x.ShownAccountId).IsUnique();
        builder.HasIndex(x => x.HiddenAccountId).IsUnique();
    }
}
