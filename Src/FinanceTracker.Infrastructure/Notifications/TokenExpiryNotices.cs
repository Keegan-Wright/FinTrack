using System.Globalization;
using FinanceTracker.Domain.Notifications;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Notifications;

/// <inheritdoc cref="ITokenExpiryNotices"/>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<ITokenExpiryNotices>]
public sealed class TokenExpiryNotices : ServiceBase<TokenExpiryNotices>, ITokenExpiryNotices
{
    /// <summary>
    /// Where tokens are made and revoked.
    /// </summary>
    public const string TokensLink = "ApiTokens";

    /// <summary>
    /// How long before a token expires the warning comes.
    /// </summary>
    public static readonly TimeSpan WeekOut = TimeSpan.FromDays(7);

    private readonly TimeProvider _timeProvider;
    private readonly INotifier _notifier;

    /// <param name="currentUser">The signed in user, or who the work runs as.</param>
    /// <param name="financeTrackerContextFactory">Makes the contexts it reads through.</param>
    /// <param name="logger">Its logger.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="notifier">Raises notifications.</param>
    public TokenExpiryNotices(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<TokenExpiryNotices> logger,
        TimeProvider timeProvider, INotifier notifier)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
        _notifier = notifier;
    }

    /// <inheritdoc/>
    public async Task NotifyAsync(CancellationToken cancellationToken)
    {
        DateTime now = _timeProvider.UtcNow;
        DateTime warnFrom = now + WeekOut;
        DateTime expiredSince = ExpiredSince(now);

        List<(Guid Id, string Name, DateTime ExpiresAt)> tokens;
        await using (FinanceTrackerContext context =
                     await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken))
        {
            tokens = (await context.PersonalAccessTokens.OwnedBy(UserId)
                    .Where(x => x.RevokedAt == null && x.ExpiresAt <= warnFrom && x.ExpiresAt > expiredSince)
                    .Select(static x => new { x.Id, x.Name, ExpiresAt = x.ExpiresAt!.Value })
                    .ToListAsync(cancellationToken))
                .Select(static x => (x.Id, x.Name, x.ExpiresAt))
                .ToList();
        }

        foreach ((Guid id, string name, DateTime expiresAt) in tokens)
        {
            await NotifyForAsync(id, name, expiresAt, now, cancellationToken);
        }
    }

    /// <summary>
    /// The earliest a token can have expired and still be told about. One that expired longer ago than read
    /// notifications are kept is passed over, so pruning its notice doesn't raise it again, see
    /// <see cref="NotificationRetention"/>.
    /// </summary>
    /// <param name="now">The time now, in UTC.</param>
    public static DateTime ExpiredSince(DateTime now) => now - NotificationRetention.ReadKeptFor;

    /// <summary>
    /// Says a token has expired, or expires soon.
    /// </summary>
    /// <param name="tokenId">The token.</param>
    /// <param name="name">Its name.</param>
    /// <param name="expiresAt">When it expires.</param>
    /// <param name="now">The time now, in UTC.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private Task<bool> NotifyForAsync(Guid tokenId, string name, DateTime expiresAt, DateTime now,
        CancellationToken cancellationToken)
    {
        if (now >= expiresAt)
        {
            return _notifier.NotifyAsync(NotificationKind.ApiTokenExpiring, $"token-expired:{tokenId:N}",
                $"API token {name} has expired",
                $"Whatever uses {name} can no longer read your data. Make a new token to carry on.", TokensLink,
                cancellationToken);
        }

        int days = Math.Max(1, (int)Math.Ceiling((expiresAt - now).TotalDays));
        string dayText = days == 1 ? "a day" : $"{days} days";
        return _notifier.NotifyAsync(NotificationKind.ApiTokenExpiring, $"token-expiring:{tokenId:N}",
            $"API token {name} expires in {dayText}",
            $"Make a new token before {LocalDate(expiresAt)} and swap it into whatever uses {name}.", TokensLink,
            cancellationToken);
    }

    /// <summary>
    /// The day, where the user is, e.g. <c>14 November</c>.
    /// </summary>
    /// <param name="utc">The time, in UTC.</param>
    private string LocalDate(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _timeProvider.LocalTimeZone)
            .ToString("d MMMM", CultureInfo.CurrentCulture);
}
