using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.Notifications;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Notifications.Email;
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

    public NotificationsService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<NotificationsService> logger,
        TimeProvider timeProvider, IMailSender mailSender)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
        _mailSender = mailSender;
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

        DomainNotificationSettings settings = await context.NotificationSettings.OwnedBy(UserId)
                                                  .SingleOrDefaultAsync(cancellationToken)
                                              ?? new DomainNotificationSettings(UserId);
        return await ToContractAsync(context, settings, cancellationToken);
    }

    public async Task<NotificationSettings> ChangeSettingsAsync(ChangeNotificationSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainNotificationSettings? stored = await context.NotificationSettings.OwnedBy(UserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (stored is null)
        {
            stored = new DomainNotificationSettings(UserId);
            context.NotificationSettings.Add(stored);
        }

        stored.EmailOnly(settings.Emailed.Select(NotificationMapper.ToDomain));
        await context.SaveChangesAsync(cancellationToken);
        return await ToContractAsync(context, stored, cancellationToken);
    }

    private async Task<NotificationSettings> ToContractAsync(FinanceTrackerContext context,
        DomainNotificationSettings settings, CancellationToken cancellationToken)
    {
        string? address = await context.Users.Where(x => x.Id == UserId).Select(static x => x.Email)
            .SingleOrDefaultAsync(cancellationToken);

        return new NotificationSettings
        {
            Emailed = settings.Emailed.Select(NotificationMapper.ToContract).ToList(),
            EmailAddress = string.IsNullOrWhiteSpace(address) ? null : address,
            EmailAvailable = _mailSender.IsAvailable
        };
    }
}
