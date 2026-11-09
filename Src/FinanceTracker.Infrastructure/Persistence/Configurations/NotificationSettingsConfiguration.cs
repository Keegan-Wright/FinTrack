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

        // The kinds turned off, see NotificationSettings
        builder.Ignore(x => x.Emailed);
        builder.Ignore(x => x.Webhooked);
        builder.PrimitiveCollection(x => x.NotEmailed).HasField("_notEmailed");
        builder.PrimitiveCollection(x => x.NotWebhooked).HasField("_notWebhooked");

        builder.Property(x => x.LastEmailFailure).HasMaxLength(200);
        builder.Property(x => x.LastWebhookFailure).HasMaxLength(200);

        // The address can carry a token, e.g. a chat app's webhook, and the secret signs posts, so both are encrypted
        builder.Property(x => x.WebhookUrl).Encrypted();
        builder.Property(x => x.WebhookSecret).Encrypted();
    }
}
