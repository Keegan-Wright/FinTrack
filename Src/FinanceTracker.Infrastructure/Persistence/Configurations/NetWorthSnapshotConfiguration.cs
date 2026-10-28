using FinanceTracker.Domain.NetWorth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class NetWorthSnapshotConfiguration : AggregateRootConfiguration<NetWorthSnapshot>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<NetWorthSnapshot> builder)
    {
        builder.ToTable("NetWorthSnapshots");

        // Not encrypted, like a balance snapshot's date
        builder.HasIndex(x => new { x.UserId, x.Date }).IsUnique();

        builder.Property(x => x.Assets).Encrypted();
        builder.Property(x => x.Liabilities).Encrypted();
        builder.Ignore(x => x.NetWorth);
    }
}
