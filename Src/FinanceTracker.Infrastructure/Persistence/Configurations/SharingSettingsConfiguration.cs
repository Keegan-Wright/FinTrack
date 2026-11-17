using FinanceTracker.Domain.Sharing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="SharingSettings"/> to its table.
/// </summary>
internal sealed class SharingSettingsConfiguration : AggregateRootConfiguration<SharingSettings>
{
    /// <inheritdoc/>
    protected override void ConfigureAggregate(EntityTypeBuilder<SharingSettings> builder)
    {
        builder.ToTable("SharingSettings");

        // One per user
        builder.HasIndex(x => x.UserId).IsUnique();
    }
}
