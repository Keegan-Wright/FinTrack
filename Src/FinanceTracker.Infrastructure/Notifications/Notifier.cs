using System.Diagnostics;
using FinanceTracker.Domain.Notifications;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Notifications.Email;
using FinanceTracker.Infrastructure.Notifications.Webhooks;
using FinanceTracker.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Notifications;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<INotifier>]
public sealed class Notifier : ServiceBase<Notifier>, INotifier
{
    private readonly INotificationPublisher _publisher;
    private readonly IMailSender _mailSender;
    private readonly INotificationEmails _emails;
    private readonly IWebhookSender _webhookSender;
    private readonly PublicAddress _publicAddress;
    private readonly TimeProvider _timeProvider;

    public Notifier(ICurrentUser currentUser, IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory,
        ILogger<Notifier> logger, INotificationPublisher publisher, IMailSender mailSender,
        INotificationEmails emails, IWebhookSender webhookSender, PublicAddress publicAddress,
        TimeProvider timeProvider)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _publisher = publisher;
        _mailSender = mailSender;
        _emails = emails;
        _webhookSender = webhookSender;
        _publicAddress = publicAddress;
        _timeProvider = timeProvider;
    }

    public async Task<bool> NotifyAsync(NotificationKind kind, string key, string title, string message,
        string? link, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        if (await context.Notifications.OwnedBy(UserId).AnyAsync(x => x.Key == key, cancellationToken))
        {
            return false;
        }

        NotificationSettings? settings = await context.NotificationSettings.OwnedBy(UserId)
            .SingleOrDefaultAsync(cancellationToken);

        bool inApp = NotificationSettings.IsInAppFor(settings, kind);
        string? address = await AddressIfEmailedAsync(context, settings, kind, cancellationToken);
        bool webhooked = settings is not null && settings.IsWebhooked(kind);

        // Turned off everywhere, so it isn't raised.
        if (!inApp && address is null && !webhooked)
        {
            return false;
        }

        Notification notification = new(UserId, kind, key, title, message, link, inApp);
        context.Notifications.Add(notification);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Raised at the same moment by something else noticing the same event, the unique key kept it to one
            return false;
        }

        Logger.LogNotified(UserId, kind, notification.Id, inApp, address is not null, webhooked);
        InfrastructureTelemetry.IncrementCurrent(TelemetryTags.NotificationsRaised);
        Activity.Current?.AddEvent(new ActivityEvent("Notified", tags: new ActivityTagsCollection
        {
            [TelemetryTags.NotificationKind] = kind.ToString(),
            [TelemetryTags.NotificationId] = notification.Id.ToString()
        }));

        if (inApp)
        {
            await _publisher.NotificationAddedAsync(UserId);
        }

        await SendOnAsync(context, settings, notification, address, webhooked, cancellationToken);
        return true;
    }

    /// <summary>
    /// Emails it and posts it to their webhook, each when they want that kind sent there. A failure is logged, the
    /// notification stands, and how each went is recorded for the Notifications page.
    /// </summary>
    /// <param name="address">Where to email it, null when it isn't emailed.</param>
    /// <param name="webhooked">Whether to post it to their webhook.</param>
    private async Task SendOnAsync(FinanceTrackerContext context, NotificationSettings? settings,
        Notification notification, string? address, bool webhooked, CancellationToken cancellationToken)
    {
        if (address is not null)
        {
            string? emailFailure = await EmailAsync(notification, address, cancellationToken);
            settings ??= AddSettings(context);
            settings.RecordEmail(_timeProvider.UtcNow, emailFailure);
        }

        // Only settings with a webhook post, see NotifyAsync
        if (webhooked && settings is not null)
        {
            string? webhookFailure = await PostAsync(settings, notification, cancellationToken);
            settings.RecordWebhook(_timeProvider.UtcNow, webhookFailure);
        }

        if (!context.ChangeTracker.HasChanges())
        {
            return;
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            // Their settings were first saved at the same moment by something else, only the record of how this
            // went is lost, what was sent and the notification stand
            Logger.LogNotificationDeliveryNotRecorded(exception, notification.Id, UserId);
        }
    }

    /// <summary>
    /// Where to email <paramref name="kind"/>: the user's address when they have that kind emailed, have a confirmed
    /// address, and a mail server is set up. Null when it isn't emailed.
    /// </summary>
    private async Task<string?> AddressIfEmailedAsync(FinanceTrackerContext context, NotificationSettings? settings,
        NotificationKind kind, CancellationToken cancellationToken)
    {
        if (!_mailSender.IsAvailable || !NotificationSettings.IsEmailedFor(settings, kind))
        {
            return null;
        }

        // Only a confirmed address, so a mistyped one doesn't get them
        string? address = await context.Users.Where(x => x.Id == UserId && x.EmailConfirmed)
            .Select(static x => x.Email)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        return address;
    }

    /// <returns>Null when it was sent, otherwise why not.</returns>
    private async Task<string?> EmailAsync(Notification notification, string address,
        CancellationToken cancellationToken)
    {
        try
        {
            await _mailSender.SendAsync(await _emails.ForAsync(notification, address), cancellationToken);
            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.LogNotificationEmailFailed(exception, notification.Id, UserId);
            return MailFailure.Describe(exception);
        }
    }

    /// <returns>Null when it was delivered, otherwise why not.</returns>
    private async Task<string?> PostAsync(NotificationSettings settings, Notification notification,
        CancellationToken cancellationToken)
    {
        try
        {
            await _webhookSender.SendAsync(settings.WebhookUrl!, settings.WebhookSecret!,
                WebhookPayload.For(notification, _publicAddress), cancellationToken);
            return null;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            Logger.LogNotificationWebhookFailed(exception, notification.Id, UserId);
            return WebhookFailure.Describe(exception);
        }
    }

    private NotificationSettings AddSettings(FinanceTrackerContext context)
    {
        NotificationSettings settings = new(UserId);
        context.NotificationSettings.Add(settings);
        return settings;
    }
}
