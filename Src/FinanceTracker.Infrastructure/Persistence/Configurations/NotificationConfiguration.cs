using FinanceTracker.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : AggregateRootConfiguration<Notification>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");

        // They name the user's banks, so they're encrypted like the bank's data. The key only holds ids and dates
        builder.Property(x => x.Title).Encrypted();
        builder.Property(x => x.Message).Encrypted();

        // One notification per event, see Notifier
        builder.HasIndex(x => new { x.UserId, x.Key }).IsUnique();

        // The bell's unread count and the newest first
        builder.HasIndex(x => new { x.UserId, x.ReadAt });
        builder.HasIndex(x => new { x.UserId, x.Created });
    }
}
