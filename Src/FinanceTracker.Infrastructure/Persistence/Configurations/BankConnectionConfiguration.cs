using FinanceTracker.Domain.OpenBanking.Connections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class BankConnectionConfiguration : AggregateRootConfiguration<BankConnection>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<BankConnection> builder)
    {
        builder.ToTable("BankConnections");

        builder.HasIndex(x => new { x.UserId, x.ExternalProviderId }).IsUnique();

        builder.PrimitiveCollection(x => x.Scopes).HasField("_scopes");

        builder.HasMany(x => x.AccessTokens)
            .WithOne()
            .HasForeignKey("ConnectionId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.AccessTokens).HasField("_accessTokens");

        builder.Ignore(x => x.ActiveToken);
    }
}
