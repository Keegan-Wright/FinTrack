using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Domain.Notifications;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.Sharing;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.OpenBanking.Transfers;
using FinanceTracker.Infrastructure.Rules;
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
        await NotifyAsync(invitee.Id, NotificationKind.SharingInvite, RoundKey("sharing-invite", link),
            $"{FullName(inviter)} wants to share with you",
            "Accept on the Sharing page to link with them. Nothing is shared until each of you chooses what to share.",
            cancellationToken);

        return InviteOutcome.Sent(ToContract(link, invitee));
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
        await NotifyAsync(link.InvitedBy, NotificationKind.SharingAccepted, RoundKey("sharing-accepted", link),
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

        await NotifyOtherAsync(context, link, RoundKey("sharing-declined", link),
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
        if (link is null || (link.Status == UserLinkStatus.Pending && link.InviteeId == UserId))
        {
            return false;
        }

        // Afterwards neither's rules reach the other's accounts
        List<(Guid OwnerId, Guid AccountId)> changeable = await ChangeableAcrossAsync(context, link, cancellationToken);

        // Each gets their own copy of a joint account back first, with its tags as they stand, while the link still
        // lets each see the other's names. Then the other person's come off with everything else of theirs
        bool splitAny = await SplitJointAccountsAsync(context, link, cancellationToken);

        bool wasLinked = link.Status == UserLinkStatus.Accepted;
        link.End(UserId, _timeProvider.GetUtcNow().UtcDateTime);

        // A household member either of them said is the other is a plain name again, kept as it is
        Guid inviterId = link.UserId;
        Guid inviteeId = link.InviteeId;
        List<Domain.Household.HouseholdMember> linkedMembers = await context.HouseholdMembers
            .Where(x => (x.UserId == inviterId && x.LinkedUserId == inviteeId) ||
                        (x.UserId == inviteeId && x.LinkedUserId == inviterId))
            .ToListAsync(cancellationToken);
        foreach (Domain.Household.HouseholdMember member in linkedMembers)
        {
            member.UnlinkUser();
        }

        // Both ways, so linking again later shares nothing until each of them chooses it again
        List<Share> shares = await context.Shares.Where(x => x.LinkId == link.Id).ToListAsync(cancellationToken);
        await TakeOffAnnotationsAsync(context, link, shares, cancellationToken);
        context.Shares.RemoveRange(shares);
        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChanged(UserId, "ended", "user link", link.Id);

        // A linked household member's accounts recognised their transfers until now, and a split copy's transfers
        // were cleared when it was merged
        if (shares.Count > 0 || linkedMembers.Count > 0 || splitAny)
        {
            await RematchTransfersAsync(context, link, cancellationToken);
        }

        await RerunRulesAsync(changeable, cancellationToken);
        await NotifyEndedAsync(context, link, wasLinked, cancellationToken);
        return true;
    }

    /// <summary>
    /// Tells the other person the link has ended, or that the invite they had was withdrawn.
    /// </summary>
    private async Task NotifyEndedAsync(FinanceTrackerContext context, DomainLink link, bool wasLinked,
        CancellationToken cancellationToken)
    {
        if (wasLinked)
        {
            await NotifyOtherAsync(context, link, RoundKey("sharing-ended", link),
                person => $"{person} stopped sharing with you",
                "Nothing of theirs is shared with you now, and nothing of yours with them. Joint accounts you'd " +
                "merged are split, each of you keeping your own copy.",
                cancellationToken, NotificationKind.SharingChanged);
            return;
        }

        await NotifyOtherAsync(context, link, RoundKey("sharing-withdrawn", link),
            person => $"{person} withdrew their invite to share", "Nothing is shared between you.",
            cancellationToken, NotificationKind.SharingChanged);
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

        Dictionary<(ShareItemType Kind, Guid ItemId), DomainShareMode> sharedBefore =
            await ShareChanges.ChosenAsync(context, link.Id, UserId, cancellationToken);

        Dictionary<Guid, DomainShareMode> wanted =
            request.Accounts.ToDictionary(static x => x.AccountId, static x => ToDomain(x.Mode));

        List<Share> existing = await OwnAccountShares(context, link.Id).ToListAsync(cancellationToken);

        // An account's bills and debts go with it
        HashSet<(ShareItemType ItemType, Guid Id)> goingWithBefore =
            await ItemShares.FollowingAccountsAsync(context, link, UserId, cancellationToken);

        // The other person's rules reach an account only while they can change it
        HashSet<Guid> changeableBefore = existing.Where(static x => x.Mode == DomainShareMode.AllowChanges)
            .Select(static x => x.ItemId)
            .ToHashSet();

        HashSet<Guid> changeableAfter = wanted.Where(static x => x.Value == DomainShareMode.AllowChanges)
            .Select(static x => x.Key)
            .ToHashSet();

        changeableBefore.SymmetricExceptWith(changeableAfter);

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
        await ItemShares.TakeOffNoLongerSharedAsync(context, link, UserId, goingWithBefore, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChanged(UserId, "changed account shares in", "user link", link.Id);

        await RematchTransfersAsync(context, link, cancellationToken);
        await RerunRulesAsync(changeableBefore.Select(x => (UserId, x)).ToList(), cancellationToken);
        await NotifySharesChangedAsync(context, link, sharedBefore, cancellationToken);

        return await LinkSharesAsync(context, link, cancellationToken);
    }

    public async Task<LinkShares> ChangeItemSharesAsync(Guid linkId, ChangeItemShares request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainLink link = await AcceptedLinkAsync(context, linkId, cancellationToken)
                          ?? throw new InvalidOperationException($"No accepted link {linkId} was found.");

        Dictionary<(ShareItemType Kind, Guid ItemId), DomainShareMode> sharedBefore =
            await ShareChanges.ChosenAsync(context, link.Id, UserId, cancellationToken);
        await ItemShares.ChangeAsync(context, link, UserId, request, _timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChanged(UserId, "changed item shares in", "user link", link.Id);
        await NotifySharesChangedAsync(context, link, sharedBefore, cancellationToken);

        return await LinkSharesAsync(context, link, cancellationToken);
    }

    public async Task<LinkShares> MergeJointAccountsAsync(Guid linkId, MergeJointAccounts request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainLink link = await AcceptedLinkAsync(context, linkId, cancellationToken)
                          ?? throw new InvalidOperationException($"No accepted link {linkId} was found.");

        List<(Guid Yours, Guid Theirs)> pairs =
            request.Accounts.Select(static x => (x.YourAccountId, x.TheirAccountId)).ToList();
        List<JointAccount> merged = await JointAccounts.MergeAsync(context, link, UserId, pairs,
            _timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        foreach (JointAccount joint in merged)
        {
            Logger.LogChanged(UserId, "merged", "joint account", joint.Id);
        }

        // Once for all of them, it matches everything both of them have
        await RematchTransfersAsync(context, link, cancellationToken);
        await RerunRulesAsync(await CopiesOfAsync(context, merged, cancellationToken), cancellationToken);

        string name = await MergedNamesAsync(context, merged, cancellationToken);
        await NotifyOtherAsync(context, link, $"joint-account:{merged[0].Id}:merged",
            person => $"{person} merged {name}",
            "You'd both connected them, so each is counted once now. You each see and tag the same copy, and yours " +
            "keeps syncing in the background. Either of you can split one again on the Sharing page.",
            cancellationToken);

        return await LinkSharesAsync(context, link, cancellationToken);
    }

    /// <summary>
    /// What was merged, for the notification, e.g. <c>your joint account Joint</c> or <c>3 joint accounts</c>.
    /// </summary>
    private static async Task<string> MergedNamesAsync(FinanceTrackerContext context, List<JointAccount> merged,
        CancellationToken cancellationToken)
    {
        if (merged.Count > 1)
        {
            return $"{merged.Count} of your joint accounts";
        }

        string name = await AccountNameAsync(context, merged[0].ShownAccountId, cancellationToken);
        return $"your joint account {name}";
    }

    public async Task<LinkShares> SplitJointAccountAsync(Guid linkId, Guid jointAccountId,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainLink link = await AcceptedLinkAsync(context, linkId, cancellationToken)
                          ?? throw new InvalidOperationException($"No accepted link {linkId} was found.");
        JointAccount joint = await context.JointAccounts
                                 .SingleOrDefaultAsync(x => x.Id == jointAccountId && x.LinkId == link.Id,
                                     cancellationToken)
                             ?? throw new InvalidOperationException($"No joint account {jointAccountId} was found.");

        string name = await AccountNameAsync(context, joint.ShownAccountId, cancellationToken);
        List<(Guid OwnerId, Guid AccountId)> copies = await CopiesOfAsync(context, [joint], cancellationToken);
        await JointAccounts.SplitAsync(context, link, joint, _timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChanged(UserId, "split", "joint account", joint.Id);

        await RematchTransfersAsync(context, link, cancellationToken);
        await RerunRulesAsync(copies, cancellationToken);

        await NotifyOtherAsync(context, link, $"joint-account:{joint.Id}:split",
            person => $"{person} split your joint account {name}",
            "You each see your own copy again, with its tags as they were. It counts in both of your totals until " +
            "you merge it again.",
            cancellationToken);

        return await LinkSharesAsync(context, link, cancellationToken);
    }

    /// <summary>
    /// Splits every joint account merged in <paramref name="link"/>, and saves.
    /// </summary>
    /// <returns>Whether there were any to split.</returns>
    private async Task<bool> SplitJointAccountsAsync(FinanceTrackerContext context, DomainLink link,
        CancellationToken cancellationToken)
    {
        List<JointAccount> joints = await context.JointAccounts.Where(x => x.LinkId == link.Id)
            .ToListAsync(cancellationToken);
        if (joints.Count == 0)
        {
            return false;
        }

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        foreach (JointAccount joint in joints)
        {
            await JointAccounts.SplitAsync(context, link, joint, now, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static async Task<string> AccountNameAsync(FinanceTrackerContext context, Guid accountId,
        CancellationToken cancellationToken) =>
        await context.BankAccounts.AsNoTracking()
            .Where(x => x.Id == accountId)
            .Select(static x => x.DisplayName)
            .SingleAsync(cancellationToken);

    /// <summary>
    /// Tells the other person in <paramref name="link"/> what the user did.
    /// </summary>
    /// <param name="title">The title, given the user's name.</param>
    private async Task NotifyOtherAsync(FinanceTrackerContext context, DomainLink link, string key,
        Func<string, string> title, string message, CancellationToken cancellationToken,
        NotificationKind kind = NotificationKind.JointAccount)
    {
        FinanceTrackerUser user = await context.Users.AsNoTracking().SingleAsync(x => x.Id == UserId,
            cancellationToken);
        await NotifyAsync(link.OtherThan(UserId), kind, key, title(FullName(user)), message, cancellationToken);
    }

    /// <summary>
    /// Tells the other person in <paramref name="link"/> what the user changed in what they share with them, when
    /// anything did.
    /// </summary>
    /// <param name="before">What the user chose to share before, see <see cref="ShareChanges.ChosenAsync"/>.</param>
    private async Task NotifySharesChangedAsync(FinanceTrackerContext context, DomainLink link,
        Dictionary<(ShareItemType Kind, Guid ItemId), DomainShareMode> before, CancellationToken cancellationToken)
    {
        Dictionary<(ShareItemType Kind, Guid ItemId), DomainShareMode> after =
            await ShareChanges.ChosenAsync(context, link.Id, UserId, cancellationToken);

        if (ShareChanges.Describe(before, after) is not { } message)
        {
            return;
        }

        await NotifyOtherAsync(context, link,
            $"sharing-changed:{link.Id:N}:{UserId:N}:{_timeProvider.GetUtcNow().UtcTicks}",
            person => $"{person} changed what they share with you", message, cancellationToken,
            NotificationKind.SharingChanged);
    }

    public async Task<SharingView> GetViewAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        return await ViewAsync(context, cancellationToken);
    }

    public async Task<SharingView> ChangeViewAsync(ChangeSharingView request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        SharingSettings? settings = await context.SharingSettings.OwnedBy(UserId).SingleOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            settings = new SharingSettings(UserId);
            context.SharingSettings.Add(settings);
        }

        settings.ShowShared(request.IncludeShared);
        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChangedTheir(UserId, "changed", "sharing view");

        return await ViewAsync(context, cancellationToken);
    }

    /// <summary>
    /// Their choice, and whether anything's shared with them whichever they chose.
    /// </summary>
    private async Task<SharingView> ViewAsync(FinanceTrackerContext context, CancellationToken cancellationToken)
    {
        SharingSettings? settings = await context.SharingSettings.OwnedBy(UserId).AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

        bool includesShared = settings?.IncludeShared ?? true;

        bool hasShared = await context.Shares.AnyAsync(x =>
            x.ItemType == ShareItemType.BankAccount && x.UserId != UserId &&
            context.UserLinks.Any(l => l.Id == x.LinkId && l.Status == UserLinkStatus.Accepted &&
                                       (l.UserId == UserId || l.InviteeId == UserId)), cancellationToken);

        return new SharingView { IncludesShared = includesShared, HasSharedAccounts = hasShared };
    }

    /// <summary>
    /// The accounts in <paramref name="link"/> one person lets the other change, so the other's rules reach them. Those
    /// shared with changes allowed, and both copies of each merged joint account. Each with its owner.
    /// </summary>
    private static async Task<List<(Guid OwnerId, Guid AccountId)>> ChangeableAcrossAsync(
        FinanceTrackerContext context, DomainLink link, CancellationToken cancellationToken)
    {
        var shared = await context.Shares.AsNoTracking()
            .Where(x => x.LinkId == link.Id && x.ItemType == ShareItemType.BankAccount &&
                        x.Mode == DomainShareMode.AllowChanges)
            .Select(static x => new { x.UserId, x.ItemId })
            .ToListAsync(cancellationToken);

        List<JointAccount> joints = await context.JointAccounts.AsNoTracking()
            .Where(x => x.LinkId == link.Id)
            .ToListAsync(cancellationToken);

        return
        [
            .. shared.Select(static x => (x.UserId, x.ItemId)),
            .. await CopiesOfAsync(context, joints, cancellationToken)
        ];
    }

    /// <summary>
    /// Both copies of each of <paramref name="joints"/>, each with its owner.
    /// </summary>
    private static async Task<List<(Guid OwnerId, Guid AccountId)>> CopiesOfAsync(FinanceTrackerContext context,
        List<JointAccount> joints, CancellationToken cancellationToken)
    {
        List<Guid> accountIds = joints.SelectMany(static x => new[] { x.ShownAccountId, x.HiddenAccountId }).ToList();
        var owners = await context.BankAccounts.AsNoTracking()
            .Where(x => accountIds.Contains(x.Id))
            .Select(static x => new { x.Id, x.UserId })
            .ToListAsync(cancellationToken);

        return owners.Select(static x => (x.UserId, x.Id)).ToList();
    }

    /// <summary>
    /// Runs the rules again over <paramref name="accounts"/>, each as its owner, once who can change them changed. Rules
    /// that no longer reach them take back what they gave, and those that now do run.
    /// </summary>
    private async Task RerunRulesAsync(List<(Guid OwnerId, Guid AccountId)> accounts,
        CancellationToken cancellationToken)
    {
        foreach (IGrouping<Guid, Guid> owner in accounts.GroupBy(static x => x.OwnerId, static x => x.AccountId))
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateUserScope(owner.Key);
            await scope.ServiceProvider.GetRequiredService<IRuleApplicationService>()
                .ReapplyToAccountsAsync(owner.Distinct().ToList(), cancellationToken);
        }
    }

    /// <summary>
    /// Matches both people's transfers again once what's shared between them changed. Money moved between their
    /// accounts is a transfer only while one of the two accounts is shared, so pairs come and go with the shares.
    /// </summary>
    private async Task RematchTransfersAsync(FinanceTrackerContext context, DomainLink link,
        CancellationToken cancellationToken)
    {
        await CrossLinkTransfers.ClearUnpairableAsync(context, link.UserId, link.InviteeId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        // Each as themselves, they're in different groups once the link has ended
        foreach (Guid userId in new[] { link.UserId, link.InviteeId })
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateUserScope(userId);
            await scope.ServiceProvider.GetRequiredService<ITransferMatchingService>().MatchAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Takes each person's tags, bills and debts off the other's transactions, those they put on while allowed to
    /// change them. Neither could see the other's afterwards, and a payment to a bill they can't see would keep their
    /// own bills from matching it.
    /// </summary>
    /// <param name="shares">The link's shares, those for what was put on each transaction among them, see
    /// <see cref="AnnotationShares"/>.</param>
    private static async Task TakeOffAnnotationsAsync(FinanceTrackerContext context, DomainLink link,
        List<Share> shares, CancellationToken cancellationToken)
    {
        foreach (Share share in shares.Where(static x => x.ItemType != ShareItemType.BankAccount))
        {
            await AnnotationShares.TakeOffTransactionsOfAsync(context, link.OtherThan(share.UserId), share.ItemType,
                share.ItemId, cancellationToken);
        }
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

        // The other person's accounts only as far as they share them. Hidden copies of joint accounts aside, the
        // shown copy stands for both
        List<Guid> theirIds = theirs.Keys.ToList();
        List<Guid> hidden = await context.JointAccounts.Select(static x => x.HiddenAccountId)
            .ToListAsync(cancellationToken);
        List<BankAccount> accounts = await context.BankAccounts.AsNoTracking()
            .Where(x => (x.UserId == UserId || (x.UserId == otherId && theirIds.Contains(x.Id))) &&
                        !hidden.Contains(x.Id))
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
            Username = other.UserName ?? string.Empty,
            Since = link.AnsweredAt ?? link.InvitedAt,
            YourAccounts = yourAccounts,
            TheirAccounts = theirAccounts,
            YourItems = await ItemShares.YoursAsync(context, link, UserId, cancellationToken),
            TheirItems = await ItemShares.TheirsAsync(context, link, UserId, cancellationToken),
            JointSuggestions = (await JointAccounts.SuggestionsAsync(context, link, UserId, cancellationToken))
                .Select(static x => new JointAccountSuggestion
                {
                    YourAccountId = x.Yours.Id,
                    YourAccountName = x.Yours.DisplayName,
                    TheirAccountId = x.Theirs.Id,
                    TheirAccountName = x.Theirs.DisplayName
                })
                .ToList(),
            JointAccounts = await MergedJointAccountsAsync(context, link, cancellationToken)
        };
    }

    /// <summary>
    /// The joint accounts merged in <paramref name="link"/>, named as the copy both see.
    /// </summary>
    private async Task<List<MergedJointAccount>> MergedJointAccountsAsync(FinanceTrackerContext context,
        DomainLink link, CancellationToken cancellationToken)
    {
        var joints = await (
                from joint in context.JointAccounts
                join shown in context.BankAccounts on joint.ShownAccountId equals shown.Id
                where joint.LinkId == link.Id
                select new { joint.Id, shown.DisplayName, shown.UserId })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return joints
            .Select(x => new MergedJointAccount
            {
                JointAccountId = x.Id, Name = x.DisplayName, ShownIsYours = x.UserId == UserId
            })
            .OrderBy(static x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static ShareMode? ModeIfShared(Dictionary<Guid, DomainShareMode> shared, Guid accountId)
    {
        if (shared.TryGetValue(accountId, out DomainShareMode mode))
        {
            return ToContract(mode);
        }

        return null;
    }

    private static ShareMode ToContract(DomainShareMode mode) => ShareModeMapping.ToContract(mode);

    private static DomainShareMode ToDomain(ShareMode mode) => ShareModeMapping.ToDomain(mode);

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

        if (link.InvitedBy == UserId)
        {
            return LinkState.Invited;
        }

        return LinkState.InvitedYou;
    }

    private static string FullName(FinanceTrackerUser user) => $"{user.FirstName} {user.LastName}".Trim();
}
