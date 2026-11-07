using FinanceTracker.Domain.Notifications;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Notifications.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Notifications;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<INotifier>]
public sealed class Notifier : ServiceBase<Notifier>, INotifier
{
    private readonly INotificationPublisher _publisher;
    private readonly IMailSender _mailSender;
    private readonly MailSettings _mailSettings;

    public Notifier(ICurrentUser currentUser, IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory,
        ILogger<Notifier> logger, INotificationPublisher publisher, IMailSender mailSender, MailSettings mailSettings)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _publisher = publisher;
        _mailSender = mailSender;
        _mailSettings = mailSettings;
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

        Notification notification = new(UserId, kind, key, title, message, link);
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

        await _publisher.NotificationAddedAsync(UserId);
        await EmailIfWantedAsync(context, notification, cancellationToken);
        return true;
    }

    /// <summary>
    /// Emails it when the user has that kind emailed, has an address, and a mail server is set up. A failed email
    /// is logged, the notification stands under the bell.
    /// </summary>
    private async Task EmailIfWantedAsync(FinanceTrackerContext context, Notification notification,
        CancellationToken cancellationToken)
    {
        if (!_mailSender.IsAvailable)
        {
            return;
        }

        NotificationSettings? settings = await context.NotificationSettings.OwnedBy(UserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!NotificationSettings.IsEmailedFor(settings, notification.Kind))
        {
            return;
        }

        string? address = await context.Users.Where(x => x.Id == UserId).Select(static x => x.Email)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        try
        {
            await _mailSender.SendAsync(NotificationEmail.For(notification, address, _mailSettings.PublicUrl),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.LogNotificationEmailFailed(exception, notification.Id, UserId);
        }
    }
}
