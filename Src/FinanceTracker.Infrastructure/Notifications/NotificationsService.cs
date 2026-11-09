using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.Notifications;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Notifications.Email;
using FinanceTracker.Infrastructure.Notifications.Webhooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DomainNotificationSettings = FinanceTracker.Domain.Notifications.NotificationSettings;

namespace FinanceTracker.Infrastructure.Notifications;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<INotificationsService>]
public sealed class NotificationsService : ServiceBase<NotificationsService>, INotificationsService
{
    /// <summary>
    /// The most a page of notifications holds.
    /// </summary>
    public const int MostAtOnce = 100;

    private readonly TimeProvider _timeProvider;
    private readonly IMailSender _mailSender;
    private readonly INotificationEmails _emails;
    private readonly IWebhookSender _webhookSender;
    private readonly WebhookSettings _webhookSettings;
    private readonly PublicAddress _publicAddress;

    public NotificationsService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<NotificationsService> logger,
        TimeProvider timeProvider, IMailSender mailSender, INotificationEmails emails, IWebhookSender webhookSender,
        WebhookSettings webhookSettings, PublicAddress publicAddress)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
        _mailSender = mailSender;
        _emails = emails;
        _webhookSender = webhookSender;
        _webhookSettings = webhookSettings;
        _publicAddress = publicAddress;
    }

    public async IAsyncEnumerable<Notification> GetNotificationsAsync(int take,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        await foreach (Domain.Notifications.Notification notification in context.Notifications.OwnedBy(UserId)
                           .OrderByDescending(static x => x.Created)
                           .Take(Math.Clamp(take, 1, MostAtOnce))
                           .AsAsyncEnumerable()
                           .WithCancellation(cancellationToken))
        {
            yield return NotificationMapper.ToContract(notification);
        }
    }

    public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Notifications.OwnedBy(UserId).CountAsync(static x => x.ReadAt == null,
            cancellationToken);
    }

    public async Task<bool> MarkReadAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        Domain.Notifications.Notification? notification = await context.Notifications.OwnedBy(UserId)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (notification is null)
        {
            return false;
        }

        notification.MarkRead(_timeProvider.UtcNow);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task MarkAllReadAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DateTime now = _timeProvider.UtcNow;
        await foreach (Domain.Notifications.Notification notification in context.Notifications.OwnedBy(UserId)
                           .Where(static x => x.ReadAt == null)
                           .AsAsyncEnumerable()
                           .WithCancellation(cancellationToken))
        {
            notification.MarkRead(now);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<NotificationSettings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainNotificationSettings settings = await FindSettingsAsync(context, cancellationToken)
                                              ?? new DomainNotificationSettings(UserId);
        return await ToContractAsync(context, settings, cancellationToken);
    }

    public async Task<NotificationSettings> ChangeSettingsAsync(ChangeNotificationSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainNotificationSettings stored = await FindOrAddSettingsAsync(context, cancellationToken);
        stored.EmailOnly(settings.Emailed.Select(NotificationMapper.ToDomain));
        stored.WebhookOnly(settings.Webhooked.Select(NotificationMapper.ToDomain));
        await context.SaveChangesAsync(cancellationToken);
        return await ToContractAsync(context, stored, cancellationToken);
    }

    public async Task<NotificationSettings?> SendTestEmailAsync(CancellationToken cancellationToken)
    {
        if (!_mailSender.IsAvailable)
        {
            return null;
        }

        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        string? address = await AddressAsync(context, cancellationToken);
        if (address is null)
        {
            return null;
        }

        string? failure = null;
        try
        {
            await _mailSender.SendAsync(await _emails.TestAsync(address), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.LogTestEmailFailed(exception, UserId);
            failure = MailFailure.Describe(exception);
        }

        DomainNotificationSettings stored = await FindOrAddSettingsAsync(context, cancellationToken);
        stored.RecordEmail(_timeProvider.UtcNow, failure);
        await context.SaveChangesAsync(cancellationToken);
        return await ToContractAsync(context, stored, cancellationToken);
    }

    public async Task<WebhookChange> SetWebhookAsync(SetWebhook webhook, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(webhook);

        Uri url = new(webhook.Url.Trim(), UriKind.Absolute);
        if (WebhookAddresses.RefusalFor(url, _webhookSettings) is { } refusal)
        {
            return new WebhookChange { Refusal = refusal };
        }

        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainNotificationSettings stored = await FindOrAddSettingsAsync(context, cancellationToken);
        stored.SetWebhook(url.ToString(), WebhookSignature.NewSecret());
        await context.SaveChangesAsync(cancellationToken);
        return new WebhookChange { Settings = await ToContractAsync(context, stored, cancellationToken) };
    }

    public async Task<NotificationSettings> RemoveWebhookAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainNotificationSettings? stored = await FindSettingsAsync(context, cancellationToken);
        if (stored is null)
        {
            return await ToContractAsync(context, new DomainNotificationSettings(UserId), cancellationToken);
        }

        stored.RemoveWebhook();
        await context.SaveChangesAsync(cancellationToken);
        return await ToContractAsync(context, stored, cancellationToken);
    }

    public async Task<NotificationSettings?> ReplaceWebhookSecretAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainNotificationSettings? stored = await FindSettingsAsync(context, cancellationToken);
        if (stored is null || !stored.ReplaceWebhookSecret(WebhookSignature.NewSecret()))
        {
            return null;
        }

        await context.SaveChangesAsync(cancellationToken);
        return await ToContractAsync(context, stored, cancellationToken);
    }

    public async Task<NotificationSettings?> SendTestWebhookAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainNotificationSettings? stored = await FindSettingsAsync(context, cancellationToken);
        if (stored is not { HasWebhook: true })
        {
            return null;
        }

        string? failure = null;
        try
        {
            await _webhookSender.SendAsync(stored.WebhookUrl!, stored.WebhookSecret!,
                WebhookPayload.Test(_timeProvider.UtcNow, _publicAddress), cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            Logger.LogTestWebhookFailed(exception, UserId);
            failure = WebhookFailure.Describe(exception);
        }

        stored.RecordWebhook(_timeProvider.UtcNow, failure);
        await context.SaveChangesAsync(cancellationToken);
        return await ToContractAsync(context, stored, cancellationToken);
    }

    private Task<DomainNotificationSettings?> FindSettingsAsync(FinanceTrackerContext context,
        CancellationToken cancellationToken) =>
        context.NotificationSettings.OwnedBy(UserId).SingleOrDefaultAsync(cancellationToken);

    private async Task<DomainNotificationSettings> FindOrAddSettingsAsync(FinanceTrackerContext context,
        CancellationToken cancellationToken)
    {
        DomainNotificationSettings? stored = await FindSettingsAsync(context, cancellationToken);
        if (stored is not null)
        {
            return stored;
        }

        stored = new DomainNotificationSettings(UserId);
        context.NotificationSettings.Add(stored);
        return stored;
    }

    private async Task<string?> AddressAsync(FinanceTrackerContext context, CancellationToken cancellationToken)
    {
        string? address = await context.Users.Where(x => x.Id == UserId).Select(static x => x.Email)
            .SingleOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(address) ? null : address;
    }

    private async Task<NotificationSettings> ToContractAsync(FinanceTrackerContext context,
        DomainNotificationSettings settings, CancellationToken cancellationToken) =>
        new()
        {
            Emailed = settings.Emailed.Select(NotificationMapper.ToContract).ToList(),
            EmailAddress = await AddressAsync(context, cancellationToken),
            EmailAvailable = _mailSender.IsAvailable,
            LastEmail = Status(settings.LastEmailAt, settings.LastEmailFailure),
            Webhooked = settings.Webhooked.Select(NotificationMapper.ToContract).ToList(),
            WebhookUrl = settings.WebhookUrl,
            WebhookSecret = settings.WebhookSecret,
            LastWebhook = Status(settings.LastWebhookAt, settings.LastWebhookFailure)
        };

    private static DeliveryStatus? Status(DateTime? at, string? failure)
    {
        if (at is not { } attempted)
        {
            return null;
        }

        return new DeliveryStatus { At = attempted, Failure = failure };
    }
}
