using FinanceTracker.Domain.OpenBanking.Connections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class AccessTokenConfiguration : IEntityTypeConfiguration<AccessToken>
{
    public void Configure(EntityTypeBuilder<AccessToken> builder)
    {
        builder.ToTable("AccessTokens");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property<Guid>("ConnectionId");

        // At most one active token per connection
        builder.HasIndex("ConnectionId")
            .IsUnique()
            .HasFilter($"\"{nameof(AccessToken.RevokedAt)}\" IS NULL");
    }
}
