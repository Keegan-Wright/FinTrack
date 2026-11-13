using FinanceTracker.Contracts;
using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Domain.Notifications;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.Sharing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DomainLink = FinanceTracker.Domain.Sharing.UserLink;
using DomainShareMode = FinanceTracker.Domain.Sharing.ShareMode;
using ShareMode = FinanceTracker.Contracts.Sharing.ShareMode;

namespace FinanceTracker.Infrastructure.Sharing;

/// <summary>
/// What each person shares in a link, see <see cref="SharingService"/>. Reading it both ways, and changing the accounts
/// and other things the user shares.
/// </summary>
internal sealed class SharingChoices : ServiceBase<SharingService>
{
    private readonly TimeProvider _timeProvider;
    private readonly SharingEffects _effects;

    public SharingChoices(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<SharingService> logger,
        TimeProvider timeProvider, SharingEffects effects)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
        _effects = effects;
    }

    public async Task<LinkShares?> GetSharesAsync(Guid linkId, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainLink? link = await SharingLinks.AcceptedLinkAsync(context, UserId, linkId, cancellationToken);
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

        DomainLink link = await SharingLinks.AcceptedLinkAsync(context, UserId, linkId, cancellationToken)
                          ?? throw new NotFoundException($"No accepted link {linkId} was found.");

        // Only their own accounts can be shared, never one shared with them
        HashSet<Guid> ownAccountIds = (await context.BankAccounts.OwnedBy(UserId)
            .Select(static x => x.Id)
            .ToListAsync(cancellationToken)).ToHashSet();
        if (request.Accounts.Any(x => !ownAccountIds.Contains(x.AccountId)))
        {
            throw new DomainRuleException("Only your own accounts can be shared.");
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

        await _effects.RematchTransfersAsync(context, link, cancellationToken);
        await _effects.RerunRulesAsync(changeableBefore.Select(x => (UserId, x)).ToList(), cancellationToken);
        await NotifySharesChangedAsync(context, link, sharedBefore, cancellationToken);

        return await LinkSharesAsync(context, link, cancellationToken);
    }

    public async Task<LinkShares> ChangeItemSharesAsync(Guid linkId, ChangeItemShares request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DomainLink link = await SharingLinks.AcceptedLinkAsync(context, UserId, linkId, cancellationToken)
                          ?? throw new NotFoundException($"No accepted link {linkId} was found.");

        Dictionary<(ShareItemType Kind, Guid ItemId), DomainShareMode> sharedBefore =
            await ShareChanges.ChosenAsync(context, link.Id, UserId, cancellationToken);
        await ItemShares.ChangeAsync(context, link, UserId, request, _timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        Logger.LogChanged(UserId, "changed item shares in", "user link", link.Id);
        await NotifySharesChangedAsync(context, link, sharedBefore, cancellationToken);

        return await LinkSharesAsync(context, link, cancellationToken);
    }

    /// <summary>
    /// What's shared both ways in <paramref name="link"/>: every account of the user's with how it's shared, and the
    /// accounts the other person shares.
    /// </summary>
    public async Task<LinkShares> LinkSharesAsync(FinanceTrackerContext context, DomainLink link,
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

        FinanceTrackerUser other = await context.Users.AsNoTracking()
            .SingleAsync(x => x.Id == otherId, cancellationToken);

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
            Name = PersonNames.Full(other),
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

        await _effects.NotifyOtherAsync(context, link,
            $"sharing-changed:{link.Id:N}:{UserId:N}:{_timeProvider.GetUtcNow().UtcTicks}",
            person => $"{person} changed what they share with you", message, cancellationToken,
            NotificationKind.SharingChanged);
    }

    /// <summary>
    /// The account shares the user made in <paramref name="linkId"/>.
    /// </summary>
    private IQueryable<Share> OwnAccountShares(FinanceTrackerContext context, Guid linkId) =>
        context.Shares.Where(x => x.LinkId == linkId && x.UserId == UserId &&
                                  x.ItemType == ShareItemType.BankAccount);

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
}
