using FinanceTracker.Domain.Notifications;
using FinanceTracker.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Notification"/> to its table.
/// </summary>
internal sealed class NotificationConfiguration : AggregateRootConfiguration<Notification>
{
    /// <inheritdoc/>
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
