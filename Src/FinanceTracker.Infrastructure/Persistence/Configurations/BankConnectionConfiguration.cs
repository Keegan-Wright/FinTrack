using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="BankConnection"/> to its table.
/// </summary>
internal sealed class BankConnectionConfiguration : AggregateRootConfiguration<BankConnection>
{
    /// <inheritdoc/>
    protected override void ConfigureAggregate(EntityTypeBuilder<BankConnection> builder)
    {
        builder.ToTable("BankConnections");

        string providerBlindIndex = builder.HasBlindIndex(x => x.ExternalProviderId);
        builder.HasIndex(nameof(BankConnection.UserId), providerBlindIndex).IsUnique();

        builder.Property(x => x.ExternalProviderId).Encrypted();
        builder.Property(x => x.Name).Encrypted();
        builder.Property(x => x.AccessCode).Encrypted();
        builder.Property(x => x.Logo).Encrypted();

        builder.Property(x => x.Scopes).HasField("_scopes").Encrypted();

        builder.HasMany(x => x.AccessTokens)
            .WithOne()
            .HasForeignKey("ConnectionId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.AccessTokens).HasField("_accessTokens");

        builder.Ignore(x => x.ActiveToken);
    }
}
