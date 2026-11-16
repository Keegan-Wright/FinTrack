using FinanceTracker.Contracts.Rules;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Sharing;
using FinanceTracker.Infrastructure.Bills;
using FinanceTracker.Infrastructure.Debts;
using FinanceTracker.Infrastructure.Sharing;
using Microsoft.EntityFrameworkCore;
using DomainRule = FinanceTracker.Domain.Rules.Rule;

namespace FinanceTracker.Infrastructure.Rules;

/// <summary>
/// The rules other people chose to share with a user, see <see cref="ShareItemType.Rule"/>. Each stays its owner's and
/// runs where their rules run. The user sees it, and edits it when allowed changes, but only with what they can see.
/// </summary>
internal static class SharedRules
{
    /// <summary>
    /// What a private account a shared rule looks at is called, its real name being its owner's to share.
    /// </summary>
    public const string PrivateAccountName = "A private account";

    /// <summary>
    /// What a tag a shared rule uses is called when it isn't shared with the user, its name being its owner's.
    /// </summary>
    public const string PrivateTagName = "A private tag";

    /// <summary>
    /// What a bill a shared rule pays is called when the user can't see it.
    /// </summary>
    public const string PrivateBillName = "A private bill";

    /// <summary>
    /// What a debt a shared rule pays is called when the user can't see it.
    /// </summary>
    public const string PrivateDebtName = "A private debt";

    /// <summary>
    /// The rules shared with <paramref name="userId"/>, untracked, each with whoever shares it and whether they can
    /// change it, by sharer then in the order they run.
    /// </summary>
    public static async Task<List<VisibleItem<DomainRule>>> WithAsync(FinanceTrackerContext context, Guid userId,
        CancellationToken cancellationToken)
    {
        var shared = await (
                from share in context.ChosenSharesWith(userId, ShareItemType.Rule)
                join rule in context.Rules on share.ItemId equals rule.Id
                where rule.UserId == share.UserId
                select new { Rule = rule, share.Mode })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        List<Guid> ownerIds = shared.Select(static x => x.Rule.UserId).Distinct().ToList();
        Dictionary<Guid, string> names = await SharedChosenItems.OwnerNamesAsync(context, ownerIds,
            cancellationToken);

        return shared
            .Select(x => new VisibleItem<DomainRule>(x.Rule, names.GetValueOrDefault(x.Rule.UserId, string.Empty),
                x.Mode == ShareMode.AllowChanges))
            .OrderBy(static x => x.SharedBy, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(static x => x.Item.Order)
            .ThenBy(static x => x.Item.Id)
            .ToList();
    }

    /// <summary>
    /// <paramref name="shared"/> as the user sees it. Who shares it, the names of what it uses, and when they can
    /// change it, the tags they can give it.
    /// </summary>
    /// <param name="seen">Every account the user can see, so those they can't are named as private.</param>
    public static async Task<Rule> ToContractAsync(FinanceTrackerContext context, Guid userId, AccountsInView seen,
        VisibleItem<DomainRule> shared, CancellationToken cancellationToken)
    {
        DomainRule rule = shared.Item;
        Rule contract = RuleMapper.ToContract(rule);
        Dictionary<Guid, string> names = await NamesAsync(context, userId, seen, rule, cancellationToken);

        Dictionary<Guid, string> tagChoices = [];
        Dictionary<Guid, string> billChoices = [];
        Dictionary<Guid, string> debtChoices = [];
        if (shared.CanChange)
        {
            tagChoices = await TagChoicesAsync(context, userId, rule, cancellationToken);
            billChoices = await BillChoicesAsync(context, userId, seen, rule, cancellationToken);
            debtChoices = await DebtChoicesAsync(context, userId, seen, rule, cancellationToken);
        }

        return new Rule
        {
            Id = contract.Id,
            Name = contract.Name,
            Order = contract.Order,
            IsEnabled = contract.IsEnabled,
            MatchMode = contract.MatchMode,
            StopProcessing = contract.StopProcessing,
            Conditions = contract.Conditions,
            Actions = contract.Actions,
            SharedBy = shared.SharedBy,
            CanChange = shared.CanChange,
            Names = names,
            TagChoices = tagChoices,
            BillChoices = billChoices,
            DebtChoices = debtChoices
        };
    }

    /// <summary>
    /// Checks an edit of <paramref name="existing"/>, someone else's rule, by <paramref name="userId"/> only uses
    /// what they may. Its owner's tags, bills and debts shared with them or already on it, and accounts they can see or
    /// it already looks at.
    /// </summary>
    /// <exception cref="InvalidOperationException">The edit uses something else.</exception>
    public static async Task EnsureAllowedAsync(FinanceTrackerContext context, Guid userId, AccountsInView seen,
        DomainRule existing, SaveRule rule, CancellationToken cancellationToken)
    {
        HashSet<Guid> tags = (await TagChoicesAsync(context, userId, existing, cancellationToken)).Keys.ToHashSet();
        if (rule.Actions.Any(x => x.ClassificationId is { } tagId && !tags.Contains(tagId)))
        {
            throw new DomainRuleException("A shared rule can only add its owner's tags shared with you.");
        }

        HashSet<Guid> bills = (await BillChoicesAsync(context, userId, seen, existing, cancellationToken)).Keys
            .ToHashSet();
        if (rule.Actions.Any(x => x.BillId is { } billId && !bills.Contains(billId)))
        {
            throw new DomainRuleException("A shared rule can only pay its owner's bills shared with you.");
        }

        HashSet<Guid> debts = (await DebtChoicesAsync(context, userId, seen, existing, cancellationToken)).Keys
            .ToHashSet();
        if (rule.Actions.Any(x => x.DebtId is { } debtId && !debts.Contains(debtId)))
        {
            throw new DomainRuleException("A shared rule can only pay off its owner's debts shared with you.");
        }

        HashSet<Guid> accounts = AccountsOf(existing).ToHashSet();
        List<Guid> named = rule.Conditions
            .Where(static x => x.Field == RuleField.Account)
            .Select(static x => Guid.Parse(x.Value))
            .Where(x => !accounts.Contains(x))
            .Distinct()
            .ToList();
        if (named.Count == 0)
        {
            return;
        }

        int visible = await context.BankAccounts.InView(seen).CountAsync(x => named.Contains(x.Id), cancellationToken);
        if (visible != named.Count)
        {
            throw new DomainRuleException("A rule can only look at the accounts you can see.");
        }
    }

    /// <summary>
    /// The tags <paramref name="userId"/> can give <paramref name="rule"/>, someone else's: its owner's tags they
    /// share with them, and those already on it.
    /// </summary>
    private static async Task<Dictionary<Guid, string>> TagChoicesAsync(FinanceTrackerContext context, Guid userId,
        DomainRule rule, CancellationToken cancellationToken)
    {
        Guid ownerId = rule.UserId;
        List<Guid> onIt = rule.Actions.Select(static x => x.ClassificationId).OfType<Guid>().ToList();
        IQueryable<Guid> shared = context.ChosenSharesWith(userId, ShareItemType.Classification)
            .Select(static x => x.ItemId);

        return await context.CustomClassifications.AsNoTracking()
            .Where(x => x.UserId == ownerId && (onIt.Contains(x.Id) || shared.Contains(x.Id)))
            .ToDictionaryAsync(static x => x.Id, static x => x.Tag, cancellationToken);
    }

    /// <summary>
    /// The bills <paramref name="userId"/> can have <paramref name="rule"/>, someone else's, pay. Its owner's bills
    /// they see, shared with them or paid from an account shared with them, and those already on it.
    /// </summary>
    private static async Task<Dictionary<Guid, string>> BillChoicesAsync(FinanceTrackerContext context, Guid userId,
        AccountsInView seen, DomainRule rule, CancellationToken cancellationToken)
    {
        Guid ownerId = rule.UserId;
        Dictionary<Guid, string> choices = (await SharedBills.ForAsync(context, userId, seen, cancellationToken))
            .Where(x => x.Item.UserId == ownerId)
            .ToDictionary(static x => x.Item.Id, static x => x.Item.Name);

        List<Guid> onIt = rule.Actions.Select(static x => x.BillId).OfType<Guid>()
            .Where(x => !choices.ContainsKey(x))
            .ToList();

        foreach (var bill in await context.Bills.AsNoTracking().Where(x => onIt.Contains(x.Id))
                     .Select(static x => new { x.Id, x.Name }).ToListAsync(cancellationToken))
        {
            choices[bill.Id] = bill.Name;
        }

        return choices;
    }

    /// <summary>
    /// The debts <paramref name="userId"/> can have <paramref name="rule"/>, someone else's, pay off. Its owner's debts
    /// they see, shared with them or following an account shared with them, and those already on it.
    /// </summary>
    private static async Task<Dictionary<Guid, string>> DebtChoicesAsync(FinanceTrackerContext context, Guid userId,
        AccountsInView seen, DomainRule rule, CancellationToken cancellationToken)
    {
        Guid ownerId = rule.UserId;
        Dictionary<Guid, string> choices = (await SharedDebts.ForAsync(context, userId, seen, cancellationToken))
            .Where(x => x.Item.UserId == ownerId)
            .ToDictionary(static x => x.Item.Id, static x => x.Item.Name);

        List<Guid> onIt = rule.Actions.Select(static x => x.DebtId).OfType<Guid>()
            .Where(x => !choices.ContainsKey(x))
            .ToList();
        foreach (var debt in await context.Debts.AsNoTracking().Where(x => onIt.Contains(x.Id))
                     .Select(static x => new { x.Id, x.Name }).ToListAsync(cancellationToken))
        {
            choices[debt.Id] = debt.Name;
        }

        return choices;
    }

    /// <summary>
    /// Those of <paramref name="tagIds"/> the user sees by name: their own, and those shared with them, whether chosen
    /// or for being on a transaction of theirs.
    /// </summary>
    private static async Task<HashSet<Guid>> SeenTagIdsAsync(FinanceTrackerContext context, Guid userId,
        List<Guid> tagIds, CancellationToken cancellationToken)
    {
        List<Guid> own = await context.CustomClassifications.OwnedBy(userId)
            .Where(x => tagIds.Contains(x.Id))
            .Select(static x => x.Id)
            .ToListAsync(cancellationToken);
        List<Guid> shared = await (
                from share in context.Shares
                join link in context.UserLinks on share.LinkId equals link.Id
                where share.ItemType == ShareItemType.Classification && tagIds.Contains(share.ItemId) &&
                      share.UserId != userId && link.Status == UserLinkStatus.Accepted &&
                      (link.UserId == userId || link.InviteeId == userId)
                select share.ItemId)
            .ToListAsync(cancellationToken);

        return [.. own, .. shared];
    }

    /// <summary>
    /// The names of the accounts, tags, bills and debts <paramref name="rule"/> uses, by id. Any the user can't see
    /// otherwise is named as private, its real name being its owner's to share.
    /// </summary>
    private static async Task<Dictionary<Guid, string>> NamesAsync(FinanceTrackerContext context, Guid userId,
        AccountsInView seen, DomainRule rule, CancellationToken cancellationToken)
    {
        List<Guid> tagIds = rule.Actions.Select(static x => x.ClassificationId).OfType<Guid>().ToList();
        List<Guid> billIds = rule.Actions.Select(static x => x.BillId).OfType<Guid>().ToList();
        List<Guid> debtIds = rule.Actions.Select(static x => x.DebtId).OfType<Guid>().ToList();
        List<Guid> accountIds = AccountsOf(rule).ToList();

        Dictionary<Guid, string> names = [];
        HashSet<Guid> seenTags = await SeenTagIdsAsync(context, userId, tagIds, cancellationToken);
        Dictionary<Guid, string> tags = await context.CustomClassifications.AsNoTracking()
            .Where(x => tagIds.Contains(x.Id))
            .ToDictionaryAsync(static x => x.Id, static x => x.Tag, cancellationToken);
        foreach ((Guid tagId, string tag) in tags)
        {
            names[tagId] = seenTags.Contains(tagId) ? tag : PrivateTagName;
        }

        if (billIds.Count > 0)
        {
            Dictionary<Guid, string> bills = (await SharedBills.ForAsync(context, userId, seen, cancellationToken))
                .ToDictionary(static x => x.Item.Id, static x => x.Item.Name);
            foreach (Guid billId in billIds)
            {
                names[billId] = bills.GetValueOrDefault(billId, PrivateBillName);
            }
        }

        if (debtIds.Count > 0)
        {
            Dictionary<Guid, string> debts = (await SharedDebts.ForAsync(context, userId, seen, cancellationToken))
                .ToDictionary(static x => x.Item.Id, static x => x.Item.Name);
            foreach (Guid debtId in debtIds)
            {
                names[debtId] = debts.GetValueOrDefault(debtId, PrivateDebtName);
            }
        }

        Dictionary<Guid, string> visible = await context.BankAccounts.InView(seen)
            .Where(x => accountIds.Contains(x.Id))
            .ToDictionaryAsync(static x => x.Id, static x => x.DisplayName, cancellationToken);
        foreach (Guid accountId in accountIds)
        {
            names[accountId] = visible.GetValueOrDefault(accountId, PrivateAccountName);
        }

        return names;
    }

    private static IEnumerable<Guid> AccountsOf(DomainRule rule) =>
        rule.Conditions
            .Where(static x => x.Field == Domain.Rules.RuleField.Account)
            .Select(static x => Guid.Parse(x.Value))
            .Distinct();
}
