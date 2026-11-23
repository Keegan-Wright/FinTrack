using FinanceTracker.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="NotificationSettings"/> to its table.
/// </summary>
internal sealed class NotificationSettingsConfiguration : AggregateRootConfiguration<NotificationSettings>
{
    /// <inheritdoc/>
    protected override void ConfigureAggregate(EntityTypeBuilder<NotificationSettings> builder)
    {
        builder.ToTable("NotificationSettings");

        // One per user
        builder.HasIndex(x => x.UserId).IsUnique();

        // The kinds turned off, see NotificationSettings
        builder.Ignore(x => x.InApp);
        builder.Ignore(x => x.Emailed);
        builder.Ignore(x => x.Webhooked);
        builder.Ignore(x => x.Pushed);
        builder.PrimitiveCollection(x => x.NotInApp).HasField("_notInApp");
        builder.PrimitiveCollection(x => x.NotEmailed).HasField("_notEmailed");
        builder.PrimitiveCollection(x => x.NotWebhooked).HasField("_notWebhooked");
        builder.PrimitiveCollection(x => x.NotPushed).HasField("_notPushed");

        builder.Property(x => x.LastEmailFailure).HasMaxLength(200);
        builder.Property(x => x.LastWebhookFailure).HasMaxLength(200);

        // The address can carry a token, e.g. a chat app's webhook, and the secret signs posts, so both are encrypted
        builder.Property(x => x.WebhookUrl).Encrypted();
        builder.Property(x => x.WebhookSecret).Encrypted();


        builder.Property(x => x.LargeTransactionOver).Encrypted();
        builder.ComplexJsonCollection(x => x.LowBalanceAlerts, alerts => alerts.Property(x => x.Below).Encrypted());
    }
}
