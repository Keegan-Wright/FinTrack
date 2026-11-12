using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.Sharing;
using Microsoft.EntityFrameworkCore;
using DomainRule = FinanceTracker.Domain.Rules.Rule;

namespace FinanceTracker.Infrastructure.Rules;

/// <summary>
/// The rules that reach each transaction. Its owner's, and those of anyone allowed to change its account, through an
/// account shared with changes allowed or a joint account they're one of the two of. The same whoever's run it is, so
/// two people's runs come to the same decisions about a transaction both of their rules reach. Rules are untracked,
/// for reading.
/// </summary>
internal sealed class RuleReach
{
    private readonly ILookup<Guid, Guid> _othersByAccount;
    private readonly ILookup<Guid, DomainRule> _rulesByPerson;

    private RuleReach(ILookup<Guid, Guid> othersByAccount, ILookup<Guid, DomainRule> rulesByPerson,
        IReadOnlyList<DomainRule> rules)
    {
        _othersByAccount = othersByAccount;
        _rulesByPerson = rulesByPerson;
        Rules = rules;
    }

    /// <summary>
    /// Every enabled rule that reaches one of the transactions.
    /// </summary>
    public IReadOnlyList<DomainRule> Rules { get; }

    /// <summary>
    /// The enabled rules that reach <paramref name="transaction"/>.
    /// </summary>
    public List<DomainRule> RulesFor(BankTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        List<DomainRule> rules = [.. _rulesByPerson[transaction.UserId]];
        foreach (Guid other in _othersByAccount[transaction.AccountId].Where(x => x != transaction.UserId))
        {
            rules.AddRange(_rulesByPerson[other]);
        }

        return rules;
    }

    /// <summary>
    /// Whether <paramref name="userId"/>'s rules reach <paramref name="transaction"/>: it's theirs, or they're allowed
    /// to change its account.
    /// </summary>
    public bool Reaches(Guid userId, BankTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        return transaction.UserId == userId || _othersByAccount[transaction.AccountId].Contains(userId);
    }

    /// <summary>
    /// Whose rule <paramref name="ruleId"/> is, among <see cref="Rules"/>.
    /// </summary>
    public Guid? OwnerOf(Guid ruleId) => Rules.FirstOrDefault(x => x.Id == ruleId)?.UserId;

    /// <summary>
    /// The rules that reach <paramref name="transactions"/>.
    /// </summary>
    public static async Task<RuleReach> ForAsync(FinanceTrackerContext context,
        IReadOnlyCollection<BankTransaction> transactions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(transactions);

        List<Guid> accountIds = transactions.Select(static x => x.AccountId).Distinct().ToList();
        List<(Guid AccountId, Guid Other)> others = [];
        others.AddRange(await ChangingThroughSharesAsync(context, accountIds, cancellationToken));
        others.AddRange(await ChangingThroughJointAccountsAsync(context, accountIds, cancellationToken));

        List<Guid> people = transactions.Select(static x => x.UserId)
            .Concat(others.Select(static x => x.Other))
            .Distinct()
            .ToList();
        List<DomainRule> rules = await context.Rules.AsNoTracking()
            .Where(x => people.Contains(x.UserId) && x.IsEnabled)
            .ToListAsync(cancellationToken);

        return new RuleReach(others.ToLookup(static x => x.AccountId, static x => x.Other),
            rules.ToLookup(static x => x.UserId), rules);
    }

    /// <summary>
    /// The other person in each accepted link sharing one of <paramref name="accountIds"/> with changes allowed, while
    /// the account is still its sharer's.
    /// </summary>
    private static async Task<List<(Guid AccountId, Guid Other)>> ChangingThroughSharesAsync(
        FinanceTrackerContext context, List<Guid> accountIds, CancellationToken cancellationToken)
    {
        var shared = await (
                from share in context.Shares
                join link in context.UserLinks on share.LinkId equals link.Id
                join account in context.BankAccounts on share.ItemId equals account.Id
                where share.ItemType == ShareItemType.BankAccount
                      && share.Mode == ShareMode.AllowChanges
                      && link.Status == UserLinkStatus.Accepted
                      && account.UserId == share.UserId
                      && accountIds.Contains(account.Id)
                select new { AccountId = account.Id, SharedBy = share.UserId, link.UserId, link.InviteeId })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return shared.Select(static x => (x.AccountId, x.SharedBy == x.UserId ? x.InviteeId : x.UserId)).ToList();
    }

    /// <summary>
    /// For each of <paramref name="accountIds"/> that's a copy of a merged joint account, the other copy's owner. Both
    /// change the shown copy, and the hidden copy stands in for it when it fills a gap.
    /// </summary>
    private static async Task<List<(Guid AccountId, Guid Other)>> ChangingThroughJointAccountsAsync(
        FinanceTrackerContext context, List<Guid> accountIds, CancellationToken cancellationToken)
    {
        var joints = await (
                from joint in context.JointAccounts
                join link in context.UserLinks on joint.LinkId equals link.Id
                join shown in context.BankAccounts on joint.ShownAccountId equals shown.Id
                join hidden in context.BankAccounts on joint.HiddenAccountId equals hidden.Id
                where link.Status == UserLinkStatus.Accepted
                      && (accountIds.Contains(shown.Id) || accountIds.Contains(hidden.Id))
                select new { ShownId = shown.Id, ShownOwner = shown.UserId, HiddenId = hidden.Id,
                    HiddenOwner = hidden.UserId })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return
        [
            .. joints.Select(static x => (x.ShownId, x.HiddenOwner)),
            .. joints.Select(static x => (x.HiddenId, x.ShownOwner))
        ];
    }
}
