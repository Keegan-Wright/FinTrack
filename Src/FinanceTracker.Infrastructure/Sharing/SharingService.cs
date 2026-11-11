using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Domain.Notifications;
using FinanceTracker.Domain.Sharing;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ContractLink = FinanceTracker.Contracts.Sharing.UserLink;
using DomainLink = FinanceTracker.Domain.Sharing.UserLink;

namespace FinanceTracker.Infrastructure.Sharing;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<ISharingService>]
public sealed class SharingService : ServiceBase<SharingService>, ISharingService
{
    /// <summary>
    /// Where in the app links are managed, for the notifications about them.
    /// </summary>
    private const string SharingPath = "Sharing";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;

    public SharingService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<SharingService> logger,
        IServiceScopeFactory scopeFactory, TimeProvider timeProvider)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
    }

    public async IAsyncEnumerable<ContractLink> GetLinksAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        List<DomainLink> links = await LiveLinksOf(context, UserId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        List<Guid> otherIds = links.Select(x => x.OtherThan(UserId)).ToList();
        Dictionary<Guid, FinanceTrackerUser> others = await context.Users
            .Where(x => otherIds.Contains(x.Id))
            .AsNoTracking()
            .ToDictionaryAsync(static x => x.Id, cancellationToken);

        foreach (DomainLink link in links.OrderBy(static x => x.InvitedAt))
        {
            yield return ToContract(link, others[link.OtherThan(UserId)]);
        }
    }

    public async Task<InviteOutcome> InviteAsync(InviteToShare request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Identity stores names and emails upper cased for lookups, so either matches in any case
        string normalised = request.Who.Trim().ToUpperInvariant();
        FinanceTrackerUser? invitee = await context.Users
            .SingleOrDefaultAsync(x => x.NormalizedUserName == normalised || x.NormalizedEmail == normalised,
                cancellationToken);

        if (invitee is null)
        {
            return InviteOutcome.Refused(InviteRefusal.NoSuchUser);
        }

        if (invitee.Id == UserId)
        {
            return InviteOutcome.Refused(InviteRefusal.Yourself);
        }

        DomainLink? existing = await LiveLinksOf(context, UserId)
            .FirstOrDefaultAsync(x => x.UserId == invitee.Id || x.InviteeId == invitee.Id, cancellationToken);

        if (existing is not null)
        {
            return InviteOutcome.Refused(RefusalFor(existing));
        }

        DomainLink link = new(UserId, invitee.Id, _timeProvider.GetUtcNow().UtcDateTime);
        context.UserLinks.Add(link);
        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChanged(UserId, "invited", "user link", link.Id);

        FinanceTrackerUser inviter = await context.Users.SingleAsync(x => x.Id == UserId, cancellationToken);
        await NotifyAsync(invitee.Id, NotificationKind.SharingInvite, $"sharing-invite:{link.Id}",
            $"{FullName(inviter)} wants to share with you",
            "Accept on the Sharing page to link with them. Nothing is shared until each of you chooses what to share.",
            cancellationToken);

        return InviteOutcome.Sent(ToContract(link, invitee));
    }

    public async Task<bool> AcceptAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainLink? link = await InviteWaitingFor(context, id, cancellationToken);
        if (link is null)
        {
            return false;
        }

        link.Accept(UserId, _timeProvider.GetUtcNow().UtcDateTime);
        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChanged(UserId, "accepted", "user link", link.Id);

        FinanceTrackerUser invitee = await context.Users.SingleAsync(x => x.Id == UserId, cancellationToken);
        await NotifyAsync(link.UserId, NotificationKind.SharingAccepted, $"sharing-accepted:{link.Id}",
            $"{FullName(invitee)} accepted your invite to share",
            "You're linked. Choose what to share with them on the Sharing page.", cancellationToken);

        return true;
    }

    public async Task<bool> DeclineAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainLink? link = await InviteWaitingFor(context, id, cancellationToken);
        if (link is null)
        {
            return false;
        }

        link.Decline(UserId, _timeProvider.GetUtcNow().UtcDateTime);
        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChanged(UserId, "declined", "user link", link.Id);
        return true;
    }

    public async Task<bool> EndAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainLink? link = await LiveLinksOf(context, UserId).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        // The person invited declines rather than ends, ending is for the inviter or either of them once linked
        if (link is null || (link.Status == UserLinkStatus.Pending && link.InviteeId == UserId))
        {
            return false;
        }

        link.End(UserId, _timeProvider.GetUtcNow().UtcDateTime);
        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChanged(UserId, "ended", "user link", link.Id);
        return true;
    }

    /// <summary>
    /// The links <paramref name="userId"/> is in, from either side, that are waiting for an answer or accepted.
    /// </summary>
    private static IQueryable<DomainLink> LiveLinksOf(FinanceTrackerContext context, Guid userId) =>
        context.UserLinks.Where(x => (x.UserId == userId || x.InviteeId == userId) &&
                                     (x.Status == UserLinkStatus.Pending || x.Status == UserLinkStatus.Accepted));

    /// <summary>
    /// The invite with <paramref name="id"/> sent to the user and still waiting for their answer.
    /// </summary>
    private Task<DomainLink?> InviteWaitingFor(FinanceTrackerContext context, Guid id,
        CancellationToken cancellationToken) =>
        context.UserLinks.SingleOrDefaultAsync(
            x => x.Id == id && x.InviteeId == UserId && x.Status == UserLinkStatus.Pending, cancellationToken);

    private static InviteRefusal RefusalFor(DomainLink existing)
    {
        if (existing.Status == UserLinkStatus.Accepted)
        {
            return InviteRefusal.AlreadyLinked;
        }

        return InviteRefusal.AlreadyInvited;
    }

    /// <summary>
    /// Raises the notification for <paramref name="userId"/>, acting as them so it follows their own settings.
    /// </summary>
    private async Task NotifyAsync(Guid userId, NotificationKind kind, string key, string title, string message,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateUserScope(userId);
        await scope.ServiceProvider.GetRequiredService<INotifier>()
            .NotifyAsync(kind, key, title, message, SharingPath, cancellationToken);
    }

    private ContractLink ToContract(DomainLink link, FinanceTrackerUser other) => new()
    {
        Id = link.Id,
        Name = FullName(other),
        Username = other.UserName ?? string.Empty,
        State = StateFor(link),
        Since = link.AnsweredAt ?? link.InvitedAt
    };

    private LinkState StateFor(DomainLink link)
    {
        if (link.Status == UserLinkStatus.Accepted)
        {
            return LinkState.Linked;
        }

        if (link.UserId == UserId)
        {
            return LinkState.Invited;
        }

        return LinkState.InvitedYou;
    }

    private static string FullName(FinanceTrackerUser user) => $"{user.FirstName} {user.LastName}".Trim();
}
