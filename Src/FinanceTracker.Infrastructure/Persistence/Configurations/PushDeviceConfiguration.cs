using FinanceTracker.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="PushDevice"/> to its table.
/// </summary>
internal sealed class PushDeviceConfiguration : AggregateRootConfiguration<PushDevice>
{
    /// <inheritdoc/>
    protected override void ConfigureAggregate(EntityTypeBuilder<PushDevice> builder)
    {
        builder.ToTable("PushDevices");

        // The endpoint is the browser's address at its push service and the keys read its messages, all encrypted.
        // One row per browser across every user, so a browser that changes hands stops getting the last user's pushes
        builder.Property(x => x.Endpoint).Encrypted();
        builder.Property(x => x.PublicKey).Encrypted();
        builder.Property(x => x.AuthSecret).Encrypted();
        builder.Property(x => x.Name).Encrypted();
        string endpointBlindIndex = builder.HasBlindIndex(x => x.Endpoint);
        builder.HasIndex(endpointBlindIndex).IsUnique();
        builder.HasIndex(x => x.UserId);

        builder.Property(x => x.LastPushFailure).HasMaxLength(200);
    }
}
