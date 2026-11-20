using System.Diagnostics;
using FinanceTracker.Domain.Notifications;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure.Notifications.Email;
using FinanceTracker.Infrastructure.Notifications.Push;
using FinanceTracker.Infrastructure.Notifications.Webhooks;
using FinanceTracker.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Notifications;

/// <inheritdoc cref="INotifier"/>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<INotifier>]
public sealed class Notifier : ServiceBase<Notifier>, INotifier
{
    private readonly INotificationPublisher _notificationPublisher;
    private readonly IMailSender _mailSender;
    private readonly INotificationEmails _notificationEmails;
    private readonly IWebhookSender _webhookSender;
    private readonly IPushSender _pushSender;
    private readonly PublicAddress _publicAddress;
    private readonly TimeProvider _timeProvider;
    private readonly FinTrackMetrics _metrics;

    /// <param name="currentUser">The signed in user, or who the work runs as.</param>
    /// <param name="financeTrackerContextFactory">Makes the contexts it reads and saves through.</param>
    /// <param name="logger">Its logger.</param>
    /// <param name="notificationPublisher">Pushes notifications to browsers.</param>
    /// <param name="mailSender">Sends the emails.</param>
    /// <param name="notificationEmails">Writes the notification emails.</param>
    /// <param name="webhookSender">Posts to webhooks.</param>
    /// <param name="pushSender">Pushes to users' devices.</param>
    /// <param name="publicAddress">The app's address as people reach it.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="metrics">Records its metrics.</param>
    public Notifier(ICurrentUser currentUser, IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory,
        ILogger<Notifier> logger, INotificationPublisher notificationPublisher, IMailSender mailSender,
        INotificationEmails notificationEmails, IWebhookSender webhookSender, IPushSender pushSender,
        PublicAddress publicAddress,
        TimeProvider timeProvider, FinTrackMetrics metrics)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _metrics = metrics;
        _notificationPublisher = notificationPublisher;
        _mailSender = mailSender;
        _notificationEmails = notificationEmails;
        _webhookSender = webhookSender;
        _pushSender = pushSender;
        _publicAddress = publicAddress;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public Task<bool> NotifyAsync(NotificationKind kind, string key, string title, string message, string? link,
        CancellationToken cancellationToken) =>
        RaiseAsync(kind, key, title, null, message, link, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> NotifyWithAmountsAsync(NotificationKind kind, string key, string title,
        string titleWithoutAmounts, string message, string? link, CancellationToken cancellationToken) =>
        RaiseAsync(kind, key, title, titleWithoutAmounts, message, link, cancellationToken);

    /// <summary>
    /// Raises it and sends it on, see <see cref="INotifier"/>.
    /// </summary>
    /// <param name="kind">What it's about.</param>
    /// <param name="key">Which event it is.</param>
    /// <param name="title">Its heading.</param>
    /// <param name="titleWithoutAmounts">Its heading without the money, null when it says none.</param>
    /// <param name="message">What it says.</param>
    /// <param name="link">Where in the app to deal with it, null for nowhere.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task<bool> RaiseAsync(NotificationKind kind, string key, string title, string? titleWithoutAmounts,
        string message, string? link, CancellationToken cancellationToken)
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
        List<PushDevice> devices = await DevicesIfPushedAsync(context, settings, kind, cancellationToken);

        // Turned off everywhere, so it isn't raised.
        if (!inApp && address is null && !webhooked && devices.Count == 0)
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

        Logger.LogNotified(UserId, kind, notification.Id, inApp, address is not null, webhooked, devices.Count > 0);
        CurrentActivity.Increment(TelemetryTags.NotificationsRaised);
        _metrics.NotificationRaised(kind.ToString());
        Activity.Current?.AddEvent(new ActivityEvent("Notified", tags: new ActivityTagsCollection
        {
            [TelemetryTags.NotificationKind] = kind.ToString(),
            [TelemetryTags.NotificationId] = notification.Id.ToString()
        }));

        if (inApp)
        {
            await _notificationPublisher.NotificationAddedAsync(UserId);
        }

        await SendOnAsync(context, settings, notification, address, webhooked, cancellationToken);
        if (devices.Count > 0)
        {
            bool showAmounts = settings?.ShowAmountsOnLockScreen == true;
            await PushAsync(context, devices, notification,
                PushMessage.For(notification, titleWithoutAmounts, showAmounts), cancellationToken);
        }

        return true;
    }

    /// <summary>
    /// The user's devices to push <paramref name="kind"/> to. None when they don't want it pushed, have no devices,
    /// or are locked, so a phone they no longer should have stops showing their money.
    /// </summary>
    /// <param name="context">The context to read through, which tracks the devices to record how each push went.</param>
    /// <param name="settings">The user's settings, null when they've never changed them.</param>
    /// <param name="kind">What it's about.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task<List<PushDevice>> DevicesIfPushedAsync(FinanceTrackerContext context,
        NotificationSettings? settings, NotificationKind kind, CancellationToken cancellationToken)
    {
        if (!NotificationSettings.IsPushedFor(settings, kind))
        {
            return [];
        }

        bool locked = await context.Users.AnyAsync(
            x => x.Id == UserId && x.LockoutEnd == FinanceTrackerUser.LockedByAdminUntil, cancellationToken);
        if (locked)
        {
            return [];
        }

        return await context.PushDevices.OwnedBy(UserId).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Pushes it to each device and records how it went. A device whose browser has gone is removed, a failure is
    /// logged and the notification stands.
    /// </summary>
    /// <param name="context">The context the devices are tracked in.</param>
    /// <param name="devices">The devices.</param>
    /// <param name="notification">The notification.</param>
    /// <param name="message">What the push shows.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task PushAsync(FinanceTrackerContext context, List<PushDevice> devices, Notification notification,
        PushMessage message, CancellationToken cancellationToken)
    {
        foreach (PushDevice device in devices)
        {
            await PushToAsync(context, device, notification, message, cancellationToken);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            // A device was removed at the same moment, only the record of how this went is lost
            Logger.LogNotificationDeliveryNotRecorded(exception, notification.Id, UserId);
        }
    }

    /// <summary>
    /// Pushes it to one device and records how it went.
    /// </summary>
    /// <param name="context">The context the device is tracked in.</param>
    /// <param name="device">The device.</param>
    /// <param name="notification">The notification.</param>
    /// <param name="message">What the push shows.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task PushToAsync(FinanceTrackerContext context, PushDevice device, Notification notification,
        PushMessage message, CancellationToken cancellationToken)
    {
        try
        {
            PushOutcome outcome = await _pushSender.SendAsync(device, message, cancellationToken);
            if (outcome == PushOutcome.Gone)
            {
                context.PushDevices.Remove(device);
                _metrics.PushDeviceRemoved();
                Logger.LogPushDeviceGone(UserId, device.Id);
                return;
            }

            _metrics.NotificationPushed(succeeded: true);
            device.RecordPush(_timeProvider.UtcNow, null);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            Logger.LogNotificationPushFailed(exception, notification.Id, UserId, device.Id);
            _metrics.NotificationPushed(succeeded: false);
            device.RecordPush(_timeProvider.UtcNow, PushFailure.Describe(exception));
        }
    }

    /// <summary>
    /// Emails it and posts it to their webhook, each when they want that kind sent there. A failure is logged, the
    /// notification stands, and how each went is recorded for the Notifications page.
    /// </summary>
    /// <param name="context">The context to save through.</param>
    /// <param name="settings">The user's settings, null when they've never changed them.</param>
    /// <param name="notification">The notification.</param>
    /// <param name="address">Where to email it, null when it isn't emailed.</param>
    /// <param name="webhooked">Whether to post it to their webhook.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task SendOnAsync(FinanceTrackerContext context, NotificationSettings? settings,
        Notification notification, string? address, bool webhooked, CancellationToken cancellationToken)
    {
        if (address is not null)
        {
            string? emailFailure = await EmailAsync(notification, address, cancellationToken);
            _metrics.NotificationEmailed(succeeded: emailFailure is null);
            settings ??= AddSettings(context);
            settings.RecordEmail(_timeProvider.UtcNow, emailFailure);
        }

        // Only settings with a webhook post, see NotifyAsync
        if (webhooked && settings is not null)
        {
            string? webhookFailure = await PostAsync(settings, notification, cancellationToken);
            _metrics.NotificationPostedToWebhook(succeeded: webhookFailure is null);
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
    /// <param name="context">The context to work through.</param>
    /// <param name="settings">The user's notification settings.</param>
    /// <param name="kind">What it's about.</param>
    /// <param name="cancellationToken">Stops the work.</param>
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

    /// <param name="notification">The notification.</param>
    /// <param name="address">Where to email it.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>Null when it was sent, otherwise why not.</returns>
    private async Task<string?> EmailAsync(Notification notification, string address,
        CancellationToken cancellationToken)
    {
        try
        {
            await _mailSender.SendAsync(await _notificationEmails.ForAsync(notification, address), cancellationToken);
            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.LogNotificationEmailFailed(exception, notification.Id, UserId);
            return MailFailure.Describe(exception);
        }
    }

    /// <param name="settings">The user's notification settings.</param>
    /// <param name="notification">The notification.</param>
    /// <param name="cancellationToken">Stops the work.</param>
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

    /// <summary>
    /// Adds settings for a user who's never changed them.
    /// </summary>
    /// <param name="context">The context to add them to.</param>
    private NotificationSettings AddSettings(FinanceTrackerContext context)
    {
        NotificationSettings settings = new(UserId);
        context.NotificationSettings.Add(settings);
        return settings;
    }
}
