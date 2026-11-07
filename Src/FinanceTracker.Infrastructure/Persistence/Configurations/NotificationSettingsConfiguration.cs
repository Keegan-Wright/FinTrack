using FinanceTracker.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class NotificationSettingsConfiguration : AggregateRootConfiguration<NotificationSettings>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<NotificationSettings> builder)
    {
        builder.ToTable("NotificationSettings");

        // One per user
        builder.HasIndex(x => x.UserId).IsUnique();

        builder.PrimitiveCollection(x => x.Emailed).HasField("_emailed");
    }
}
