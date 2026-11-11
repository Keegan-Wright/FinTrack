using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Domain.Notifications;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Domain.Sharing;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ContractLink = FinanceTracker.Contracts.Sharing.UserLink;
using DomainLink = FinanceTracker.Domain.Sharing.UserLink;
using DomainShareMode = FinanceTracker.Domain.Sharing.ShareMode;
using ShareMode = FinanceTracker.Contracts.Sharing.ShareMode;

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

        // Counted per link and per person sharing, each side's own shares only ever being its own accounts
        List<Guid> linkIds = links.Select(static x => x.Id).ToList();
        var shareCounts = await context.Shares.AsNoTracking()
            .Where(x => linkIds.Contains(x.LinkId) && x.ItemType == ShareItemType.BankAccount)
            .GroupBy(static x => new { x.LinkId, x.UserId })
            .Select(static x => new { x.Key.LinkId, x.Key.UserId, Count = x.Count() })
            .ToListAsync(cancellationToken);

        List<Guid> otherIds = links.Select(x => x.OtherThan(UserId)).ToList();
        Dictionary<Guid, FinanceTrackerUser> others = await context.Users
            .Where(x => otherIds.Contains(x.Id))
            .AsNoTracking()
            .ToDictionaryAsync(static x => x.Id, cancellationToken);

        foreach (DomainLink link in links.OrderBy(static x => x.InvitedAt))
        {
            Guid otherId = link.OtherThan(UserId);
            yield return ToContract(link, others[otherId]) with
            {
                AccountsYouShare = shareCounts.Where(x => x.LinkId == link.Id && x.UserId == UserId).Sum(static x => x.Count),
                AccountsTheyShare = shareCounts.Where(x => x.LinkId == link.Id && x.UserId == otherId).Sum(static x => x.Count)
            };
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

        // Both ways, so linking again later shares nothing until each of them chooses it again
        context.Shares.RemoveRange(await context.Shares.Where(x => x.LinkId == link.Id).ToListAsync(cancellationToken));
        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChanged(UserId, "ended", "user link", link.Id);
        return true;
    }

    public async Task<LinkShares?> GetSharesAsync(Guid linkId, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainLink? link = await AcceptedLinkAsync(context, linkId, cancellationToken);
        if (link is null)
        {
            return null;
        }

        return await LinkSharesAsync(context, link, cancellationToken);
    }

    public async Task<LinkShares> ChangeAccountSharesAsync(Guid linkId, ChangeAccountShares request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainLink link = await AcceptedLinkAsync(context, linkId, cancellationToken)
                          ?? throw new InvalidOperationException($"No accepted link {linkId} was found.");

        // Only their own accounts can be shared, never one shared with them
        HashSet<Guid> ownAccountIds = (await context.BankAccounts.OwnedBy(UserId)
            .Select(static x => x.Id)
            .ToListAsync(cancellationToken)).ToHashSet();
        if (request.Accounts.Any(x => !ownAccountIds.Contains(x.AccountId)))
        {
            throw new InvalidOperationException("Only your own accounts can be shared.");
        }

        Dictionary<Guid, DomainShareMode> wanted =
            request.Accounts.ToDictionary(static x => x.AccountId, static x => ToDomain(x.Mode));
        List<Share> existing = await OwnAccountShares(context, link.Id).ToListAsync(cancellationToken);

        foreach (Share share in existing)
        {
            if (wanted.Remove(share.ItemId, out DomainShareMode mode))
            {
                share.ChangeMode(mode);
            }
            else
            {
                context.Shares.Remove(share);
            }
        }

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        foreach ((Guid accountId, DomainShareMode mode) in wanted)
        {
            context.Shares.Add(new Share(UserId, link.Id, ShareItemType.BankAccount, accountId, mode, now));
        }

        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChanged(UserId, "changed account shares in", "user link", link.Id);

        return await LinkSharesAsync(context, link, cancellationToken);
    }

    /// <summary>
    /// The user's accepted link with <paramref name="linkId"/>, from either side.
    /// </summary>
    private Task<DomainLink?> AcceptedLinkAsync(FinanceTrackerContext context, Guid linkId,
        CancellationToken cancellationToken) =>
        context.UserLinks.SingleOrDefaultAsync(
            x => x.Id == linkId && (x.UserId == UserId || x.InviteeId == UserId) &&
                 x.Status == UserLinkStatus.Accepted, cancellationToken);

    /// <summary>
    /// The account shares the user made in <paramref name="linkId"/>.
    /// </summary>
    private IQueryable<Share> OwnAccountShares(FinanceTrackerContext context, Guid linkId) =>
        context.Shares.Where(x => x.LinkId == linkId && x.UserId == UserId &&
                                  x.ItemType == ShareItemType.BankAccount);

    /// <summary>
    /// What's shared both ways in <paramref name="link"/>: every account of the user's with how it's shared, and the
    /// accounts the other person shares.
    /// </summary>
    private async Task<LinkShares> LinkSharesAsync(FinanceTrackerContext context, DomainLink link,
        CancellationToken cancellationToken)
    {
        Guid otherId = link.OtherThan(UserId);

        List<Share> shares = await context.Shares.AsNoTracking()
            .Where(x => x.LinkId == link.Id && x.ItemType == ShareItemType.BankAccount)
            .ToListAsync(cancellationToken);
        Dictionary<Guid, DomainShareMode> yours = shares.Where(x => x.UserId == UserId)
            .ToDictionary(static x => x.ItemId, static x => x.Mode);
        Dictionary<Guid, DomainShareMode> theirs = shares.Where(x => x.UserId == otherId)
            .ToDictionary(static x => x.ItemId, static x => x.Mode);

        // The other person's accounts only as far as they share them
        List<Guid> theirIds = theirs.Keys.ToList();
        List<BankAccount> accounts = await context.BankAccounts.AsNoTracking()
            .Where(x => x.UserId == UserId || (x.UserId == otherId && theirIds.Contains(x.Id)))
            .ToListAsync(cancellationToken);

        List<Guid> connectionIds = accounts.Select(static x => x.ConnectionId).Distinct().ToList();
        Dictionary<Guid, string> bankNames = await context.BankConnections.AsNoTracking()
            .Where(x => connectionIds.Contains(x.Id))
            .ToDictionaryAsync(static x => x.Id, static x => x.Name, cancellationToken);

        FinanceTrackerUser other = await context.Users.AsNoTracking().SingleAsync(x => x.Id == otherId, cancellationToken);

        List<ShareableAccount> yourAccounts = accounts.Where(x => x.UserId == UserId)
            .OrderBy(x => bankNames.GetValueOrDefault(x.ConnectionId)).ThenBy(static x => x.DisplayName)
            .Select(x => new ShareableAccount
            {
                AccountId = x.Id,
                Name = x.DisplayName,
                BankName = bankNames.GetValueOrDefault(x.ConnectionId) ?? string.Empty,
                Mode = ModeIfShared(yours, x.Id)
            })
            .ToList();

        List<AccountSharedWithYou> theirAccounts = accounts.Where(x => x.UserId == otherId)
            .OrderBy(x => bankNames.GetValueOrDefault(x.ConnectionId)).ThenBy(static x => x.DisplayName)
            .Select(x => new AccountSharedWithYou
            {
                Name = x.DisplayName,
                BankName = bankNames.GetValueOrDefault(x.ConnectionId) ?? string.Empty,
                Mode = ToContract(theirs[x.Id])
            })
            .ToList();

        return new LinkShares
        {
            LinkId = link.Id,
            Name = FullName(other),
            YourAccounts = yourAccounts,
            TheirAccounts = theirAccounts
        };
    }

    private static ShareMode? ModeIfShared(Dictionary<Guid, DomainShareMode> shared, Guid accountId)
    {
        if (shared.TryGetValue(accountId, out DomainShareMode mode))
        {
            return ToContract(mode);
        }

        return null;
    }

    private static ShareMode ToContract(DomainShareMode mode) => mode switch
    {
        DomainShareMode.ReadOnly => ShareMode.ReadOnly,
        DomainShareMode.AllowChanges => ShareMode.AllowChanges,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
    };

    private static DomainShareMode ToDomain(ShareMode mode) => mode switch
    {
        ShareMode.ReadOnly => DomainShareMode.ReadOnly,
        ShareMode.AllowChanges => DomainShareMode.AllowChanges,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
    };

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
