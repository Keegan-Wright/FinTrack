using System.Globalization;
using FinanceTracker.Domain.Notifications;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Notifications;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IConsentNotices>]
public sealed class ConsentNotices : ServiceBase<ConsentNotices>, IConsentNotices
{
    /// <summary>
    /// Where a connection is renewed.
    /// </summary>
    public const string RenewLink = "ProviderSetup";

    /// <summary>
    /// How long before consent ends the first warning comes.
    /// </summary>
    public static readonly TimeSpan WeekOut = TimeSpan.FromDays(7);

    /// <summary>
    /// How long before consent ends the last warning comes.
    /// </summary>
    public static readonly TimeSpan DayOut = TimeSpan.FromDays(1);

    private readonly TimeProvider _timeProvider;
    private readonly INotifier _notifier;

    public ConsentNotices(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<ConsentNotices> logger,
        TimeProvider timeProvider, INotifier notifier)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
        _notifier = notifier;
    }

    public async Task NotifyAsync(CancellationToken cancellationToken)
    {
        DateTime now = _timeProvider.UtcNow;
        DateTime warnFrom = now + WeekOut;

        List<(Guid Id, string Name, DateTime EndsAt)> connections;
        await using (FinanceTrackerContext context =
                     await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken))
        {
            connections = (await context.BankConnections.OwnedBy(UserId)
                    .Where(x => x.ConsentExpiresAt <= warnFrom)
                    .Select(static x => new { x.Id, x.Name, x.ConsentExpiresAt })
                    .ToListAsync(cancellationToken))
                .Select(static x => (x.Id, x.Name, x.ConsentExpiresAt))
                .ToList();
        }

        foreach ((Guid id, string name, DateTime endsAt) in connections)
        {
            await NotifyForAsync(id, name, endsAt, now, cancellationToken);
        }
    }

    private Task<bool> NotifyForAsync(Guid connectionId, string name, DateTime endsAt, DateTime now,
        CancellationToken cancellationToken)
    {
        // Keyed by the connection and the day this consent ends, so renewing, which moves it, warns afresh
        string consent = $"{connectionId:N}:{endsAt:yyyyMMdd}";

        if (now >= endsAt)
        {
            return _notifier.NotifyAsync(NotificationKind.ConsentEnded, $"consent-ended:{consent}",
                $"{name} access has ended",
                $"FinTrack can no longer sync {name}. Renew access to start syncing it again.",
                RenewLink, cancellationToken);
        }

        string endDate = LocalDate(endsAt);
        if (endsAt - now <= DayOut)
        {
            return _notifier.NotifyAsync(NotificationKind.ConsentEndingSoon, $"consent-ending-day:{consent}",
                $"{name} access ends within a day",
                $"Renew access before {endDate} to keep {name} syncing.", RenewLink, cancellationToken);
        }

        int days = (int)Math.Ceiling((endsAt - now).TotalDays);
        return _notifier.NotifyAsync(NotificationKind.ConsentEndingSoon, $"consent-ending-week:{consent}",
            $"{name} access ends in {days} days",
            $"Renew access before {endDate} to keep {name} syncing.", RenewLink, cancellationToken);
    }

    // The day as the app's users see it, in the app's time zone and culture
    private string LocalDate(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _timeProvider.LocalTimeZone)
            .ToString("d MMMM", CultureInfo.CurrentCulture);
}
