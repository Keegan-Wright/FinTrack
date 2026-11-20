using FinanceTracker.Infrastructure.Notifications.Push;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="StoredVapidKeys"/> to its table.
/// </summary>
internal sealed class StoredVapidKeysConfiguration : IEntityTypeConfiguration<StoredVapidKeys>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<StoredVapidKeys> builder)
    {
        builder.ToTable("VapidKeys");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        // The public key is handed to every browser, the private key signs pushes
        builder.Property(x => x.PrivateKey).Encrypted();
    }
}
