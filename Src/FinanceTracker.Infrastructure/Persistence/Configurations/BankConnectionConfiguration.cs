using FinanceTracker.Domain.OpenBanking.Connections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class BankConnectionConfiguration : AggregateRootConfiguration<BankConnection>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<BankConnection> builder)
    {
        builder.ToTable("BankConnections");

        string providerBlindIndex = builder.HasBlindIndex(x => x.ExternalProviderId);
        builder.HasIndex(nameof(BankConnection.UserId), providerBlindIndex).IsUnique();

        builder.PrimitiveCollection(x => x.Scopes).HasField("_scopes");

        builder.Property(x => x.ExternalProviderId).Encrypted();
        builder.Property(x => x.Name).Encrypted();
        builder.Property(x => x.AccessCode).Encrypted();
        builder.Property(x => x.Logo).Encrypted();
        builder.Property(x => x.Scopes).Encrypted();

        builder.HasMany(x => x.AccessTokens)
            .WithOne()
            .HasForeignKey("ConnectionId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.AccessTokens).HasField("_accessTokens");

        builder.Ignore(x => x.ActiveToken);
    }
}
