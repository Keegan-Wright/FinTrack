using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Domain.Household;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Domain.Notifications;
using FinanceTracker.Domain.Sharing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ContractLink = FinanceTracker.Contracts.Sharing.UserLink;
using DomainLink = FinanceTracker.Domain.Sharing.UserLink;

namespace FinanceTracker.Infrastructure.Sharing;

/// <summary>
/// The links between people, see <see cref="SharingService"/>. Listing them, inviting, answering and ending. Ending
/// splits their joint accounts and takes off everything shared, see <see cref="EndAsync"/>.
/// </summary>
internal sealed class SharingLinks : ServiceBase<SharingService>
{
    private readonly TimeProvider _timeProvider;
    private readonly SharingEffects _effects;
    private readonly SharingJointAccounts _jointAccounts;

    public SharingLinks(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<SharingService> logger,
        TimeProvider timeProvider, SharingEffects effects, SharingJointAccounts jointAccounts)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
        _effects = effects;
        _jointAccounts = jointAccounts;
    }

    public async IAsyncEnumerable<ContractLink> GetLinksAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        List<DomainLink> links = await LiveLinksOf(context, UserId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Counted per link and per person sharing, accounts apart from the other things they chose to share
        List<Guid> linkIds = links.Select(static x => x.Id).ToList();
        var shareCounts = await context.Shares.AsNoTracking()
            .Where(x => linkIds.Contains(x.LinkId) && x.Reason == ShareReason.Chosen)
            .GroupBy(static x => new { x.LinkId, x.UserId, IsAccount = x.ItemType == ShareItemType.BankAccount })
            .Select(static x => new { x.Key.LinkId, x.Key.UserId, x.Key.IsAccount, Count = x.Count() })
            .ToListAsync(cancellationToken);

        List<Guid> otherIds = links.Select(x => x.OtherThan(UserId)).ToList();
        Dictionary<Guid, FinanceTrackerUser> others = await context.Users
            .Where(x => otherIds.Contains(x.Id))
            .AsNoTracking()
            .ToDictionaryAsync(static x => x.Id, cancellationToken);

        Dictionary<Guid, int> jointCounts = await context.JointAccounts.AsNoTracking()
            .Where(x => linkIds.Contains(x.LinkId))
            .GroupBy(static x => x.LinkId)
            .ToDictionaryAsync(static x => x.Key, static x => x.Count(), cancellationToken);

        foreach (DomainLink link in links.OrderBy(static x => x.InvitedAt))
        {
            Guid otherId = link.OtherThan(UserId);
            int suggestions = 0;
            if (link.Status == UserLinkStatus.Accepted)
            {
                suggestions = (await JointAccounts.SuggestionsAsync(context, link, UserId, cancellationToken)).Count;
            }

            yield return ToContract(link, others[otherId]) with
            {
                AccountsYouShare = shareCounts.Where(x => x.LinkId == link.Id && x.UserId == UserId && x.IsAccount)
                    .Sum(static x => x.Count),
                AccountsTheyShare = shareCounts.Where(x => x.LinkId == link.Id && x.UserId == otherId && x.IsAccount)
                    .Sum(static x => x.Count),
                ItemsYouShare = shareCounts.Where(x => x.LinkId == link.Id && x.UserId == UserId && !x.IsAccount)
                    .Sum(static x => x.Count),
                ItemsTheyShare = shareCounts.Where(x => x.LinkId == link.Id && x.UserId == otherId && !x.IsAccount)
                    .Sum(static x => x.Count),
                JointAccounts = jointCounts.GetValueOrDefault(link.Id),
                JointSuggestions = suggestions
            };
        }
    }

    public async IAsyncEnumerable<ContractLink> GetPeopleAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        List<DomainLink> links = await LiveLinksOf(context, UserId)
            .Where(static x => x.Status == UserLinkStatus.Accepted)
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

        FinanceTrackerUser? invitee = await InviteeAsync(context, request.Who, cancellationToken);

        if (invitee is null)
        {
            return InviteOutcome.Refused(InviteRefusal.NoSuchUser);
        }

        if (invitee.Id == UserId)
        {
            return InviteOutcome.Refused(InviteRefusal.Yourself);
        }

        // Their one link, whoever invited whom, opened again when it was declined, withdrawn or ended
        DomainLink? link = await PairsLinkAsync(context, invitee.Id, cancellationToken);
        if (link is { IsLive: true })
        {
            return InviteOutcome.Refused(RefusalFor(link));
        }

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        if (link is null)
        {
            link = new DomainLink(UserId, invitee.Id, now);
            context.UserLinks.Add(link);
        }
        else
        {
            link.InviteAgain(UserId, now);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // They invited the user at the same moment, and theirs was saved first, see
            // UserLinkConfiguration.OneLinkPerPairIndex, or the row version for a link opened again. Refused as if
            // theirs had been there all along
            if (await LiveLinkWithAsync(invitee.Id, cancellationToken) is not { } theirs)
            {
                throw;
            }

            return InviteOutcome.Refused(RefusalFor(theirs));
        }

        Logger.LogChanged(UserId, "invited", "user link", link.Id);

        FinanceTrackerUser inviter = await context.Users.SingleAsync(x => x.Id == UserId, cancellationToken);
        await _effects.NotifyAsync(invitee.Id, NotificationKind.SharingInvite, RoundKey("sharing-invite", link),
            $"{PersonNames.Full(inviter)} wants to share with you",
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
        await _effects.NotifyAsync(link.InvitedBy, NotificationKind.SharingAccepted,
            RoundKey("sharing-accepted", link), $"{PersonNames.Full(invitee)} accepted your invite to share",
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

        await _effects.NotifyOtherAsync(context, link, RoundKey("sharing-declined", link),
            person => $"{person} declined your invite to share", "Nothing is shared between you.",
            cancellationToken, NotificationKind.SharingChanged);
        return true;
    }

    public async Task<bool> EndAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainLink? link = await LiveLinksOf(context, UserId).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        // The person invited declines rather than ends, ending is for the inviter or either of them once linked
        if (link is null || (link.Status == UserLinkStatus.Pending && link.InvitedUserId == UserId))
        {
            return false;
        }

        // Afterwards neither's rules reach the other's accounts
        List<(Guid OwnerId, Guid AccountId)> changeable =
            await SharingEffects.ChangeableAcrossAsync(context, link, cancellationToken);

        // Each gets their own copy of a joint account back first, with its tags as they stand, while the link still
        // lets each see the other's names. Then the other person's come off with everything else of theirs
        bool splitAny = await _jointAccounts.SplitAllAsync(context, link, cancellationToken);

        bool wasLinked = link.Status == UserLinkStatus.Accepted;
        link.End(UserId, _timeProvider.GetUtcNow().UtcDateTime);

        // A household member either of them said is the other is a plain name again, kept as it is
        Guid inviterId = link.UserId;
        Guid inviteeId = link.InviteeId;
        List<HouseholdMember> linkedMembers = await context.HouseholdMembers
            .Where(x => (x.UserId == inviterId && x.LinkedUserId == inviteeId) ||
                        (x.UserId == inviteeId && x.LinkedUserId == inviterId))
            .ToListAsync(cancellationToken);
        foreach (HouseholdMember member in linkedMembers)
        {
            member.UnlinkUser();
        }

        // Both ways, so linking again later shares nothing until each of them chooses it again
        List<Share> shares = await context.Shares.Where(x => x.LinkId == link.Id).ToListAsync(cancellationToken);
        await SharingEffects.TakeOffAnnotationsAsync(context, link, shares, cancellationToken);
        context.Shares.RemoveRange(shares);
        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChanged(UserId, "ended", "user link", link.Id);

        // A linked household member's accounts recognised their transfers until now, and a split copy's transfers
        // were cleared when it was merged
        if (shares.Count > 0 || linkedMembers.Count > 0 || splitAny)
        {
            await _effects.RematchTransfersAsync(context, link, cancellationToken);
        }

        await _effects.RerunRulesAsync(changeable, cancellationToken);
        await NotifyEndedAsync(context, link, wasLinked, cancellationToken);
        return true;
    }

    /// <summary>
    /// <paramref name="userId"/>'s accepted link with <paramref name="linkId"/>, from either side.
    /// </summary>
    public static Task<DomainLink?> AcceptedLinkAsync(FinanceTrackerContext context, Guid userId, Guid linkId,
        CancellationToken cancellationToken) =>
        context.UserLinks.SingleOrDefaultAsync(
            x => x.Id == linkId && (x.UserId == userId || x.InviteeId == userId) &&
                 x.Status == UserLinkStatus.Accepted, cancellationToken);

    /// <summary>
    /// Tells the other person the link has ended, or that the invite they had was withdrawn.
    /// </summary>
    private async Task NotifyEndedAsync(FinanceTrackerContext context, DomainLink link, bool wasLinked,
        CancellationToken cancellationToken)
    {
        if (wasLinked)
        {
            await _effects.NotifyOtherAsync(context, link, RoundKey("sharing-ended", link),
                person => $"{person} stopped sharing with you",
                "Nothing of theirs is shared with you now, and nothing of yours with them. Joint accounts you'd " +
                "merged are split, each of you keeping your own copy.",
                cancellationToken, NotificationKind.SharingChanged);
            return;
        }

        await _effects.NotifyOtherAsync(context, link, RoundKey("sharing-withdrawn", link),
            person => $"{person} withdrew their invite to share", "Nothing is shared between you.",
            cancellationToken, NotificationKind.SharingChanged);
    }

    /// <summary>
    /// The user <paramref name="who"/> names, by username or email in any case. A username that's also someone
    /// else's email is the username's, as that's what's shown to people.
    /// </summary>
    private static async Task<FinanceTrackerUser?> InviteeAsync(FinanceTrackerContext context, string who,
        CancellationToken cancellationToken)
    {
        // Identity stores names and emails upper cased for lookups
        string normalised = who.Trim().ToUpperInvariant();
        List<FinanceTrackerUser> named = await context.Users
            .Where(x => x.NormalizedUserName == normalised || x.NormalizedEmail == normalised)
            .ToListAsync(cancellationToken);

        return named.Find(x => x.NormalizedUserName == normalised) ??
               named.Find(x => x.NormalizedEmail == normalised);
    }

    /// <summary>
    /// The user's one link with <paramref name="otherId"/>, live or not, null when they've never had one.
    /// </summary>
    private async Task<DomainLink?> PairsLinkAsync(FinanceTrackerContext context, Guid otherId,
        CancellationToken cancellationToken)
    {
        (Guid first, Guid second) = DomainLink.PairOf(UserId, otherId);
        return await context.UserLinks
            .SingleOrDefaultAsync(x => x.FirstOfPair == first && x.SecondOfPair == second, cancellationToken);
    }

    /// <summary>
    /// A notification's key for one round of <paramref name="link"/>, from its invite, so inviting again after it's
    /// over is told afresh, e.g. <c>sharing-invite:{link}:{invited at}</c>.
    /// </summary>
    private static string RoundKey(string what, DomainLink link) => $"{what}:{link.Id:N}:{link.InvitedAt.Ticks}";

    /// <summary>
    /// The user's live link with <paramref name="otherId"/>, read afresh, null when there's none.
    /// </summary>
    private async Task<DomainLink?> LiveLinkWithAsync(Guid otherId, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        return await LiveLinksOf(context, UserId)
            .FirstOrDefaultAsync(x => x.UserId == otherId || x.InviteeId == otherId, cancellationToken);
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
            x => x.Id == id && x.Status == UserLinkStatus.Pending && x.InvitedBy != UserId &&
                 (x.UserId == UserId || x.InviteeId == UserId), cancellationToken);

    private static InviteRefusal RefusalFor(DomainLink existing)
    {
        if (existing.Status == UserLinkStatus.Accepted)
        {
            return InviteRefusal.AlreadyLinked;
        }

        return InviteRefusal.AlreadyInvited;
    }

    private ContractLink ToContract(DomainLink link, FinanceTrackerUser other) => new()
    {
        Id = link.Id,
        Name = PersonNames.Full(other),
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

        if (link.InvitedBy == UserId)
        {
            return LinkState.Invited;
        }

        return LinkState.InvitedYou;
    }
}
