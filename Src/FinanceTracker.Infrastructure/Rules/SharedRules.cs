using FinanceTracker.Contracts.Rules;
using FinanceTracker.Domain.Sharing;
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
    /// The rules shared with <paramref name="userId"/>, untracked, each with whoever shares it and whether they can
    /// change it, by sharer then in the order they run.
    /// </summary>
    public static async Task<List<SharedRule>> WithAsync(FinanceTrackerContext context, Guid userId,
        CancellationToken cancellationToken)
    {
        var shared = await (
                from share in context.ChosenSharesWith(userId, ShareItemType.Rule)
                join rule in context.Rules on share.ItemId equals rule.Id
                join owner in context.Users on rule.UserId equals owner.Id
                where rule.UserId == share.UserId
                select new { Rule = rule, owner.FirstName, owner.LastName, share.Mode })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return shared
            .Select(static x => new SharedRule(x.Rule, $"{x.FirstName} {x.LastName}".Trim(),
                x.Mode == ShareMode.AllowChanges))
            .OrderBy(static x => x.SharedBy, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(static x => x.Rule.Order)
            .ThenBy(static x => x.Rule.Id)
            .ToList();
    }

    /// <summary>
    /// <paramref name="shared"/> as the user sees it. Who shares it, the names of what it uses, and when they can
    /// change it, the tags they can give it.
    /// </summary>
    /// <param name="seen">Every account the user can see, so those they can't are named as private.</param>
    public static async Task<Rule> ToContractAsync(FinanceTrackerContext context, Guid userId, AccountsInView seen,
        SharedRule shared, CancellationToken cancellationToken)
    {
        DomainRule rule = shared.Rule;
        Rule contract = RuleMapper.ToContract(rule);
        Dictionary<Guid, string> names = await NamesAsync(context, seen, rule, cancellationToken);

        Dictionary<Guid, string> tagChoices = [];
        if (shared.CanChange)
        {
            tagChoices = await TagChoicesAsync(context, userId, rule, cancellationToken);
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
            TagChoices = tagChoices
        };
    }

    /// <summary>
    /// Checks an edit of <paramref name="existing"/>, someone else's rule, by <paramref name="userId"/> only uses
    /// what they may. Its owner's tags shared with them or already on it, the bills and debts already on it, and
    /// accounts they can see or it already looks at.
    /// </summary>
    /// <exception cref="InvalidOperationException">The edit uses something else.</exception>
    public static async Task EnsureAllowedAsync(FinanceTrackerContext context, Guid userId, AccountsInView seen,
        DomainRule existing, SaveRule rule, CancellationToken cancellationToken)
    {
        HashSet<Guid> tags = (await TagChoicesAsync(context, userId, existing, cancellationToken)).Keys.ToHashSet();
        if (rule.Actions.Any(x => x.ClassificationId is { } tagId && !tags.Contains(tagId)))
        {
            throw new InvalidOperationException("A shared rule can only add its owner's tags shared with you.");
        }

        HashSet<Guid> bills = existing.Actions.Select(static x => x.BillId).OfType<Guid>().ToHashSet();
        HashSet<Guid> debts = existing.Actions.Select(static x => x.DebtId).OfType<Guid>().ToHashSet();
        if (rule.Actions.Any(x => (x.BillId is { } billId && !bills.Contains(billId)) ||
                                  (x.DebtId is { } debtId && !debts.Contains(debtId))))
        {
            throw new InvalidOperationException("A shared rule can only keep the bills and debts it already pays.");
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
            throw new InvalidOperationException("A rule can only look at the accounts you can see.");
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
    /// The names of the accounts, tags, bills and debts <paramref name="rule"/> uses, by id. An account the user can't
    /// see is named as private.
    /// </summary>
    private static async Task<Dictionary<Guid, string>> NamesAsync(FinanceTrackerContext context,
        AccountsInView seen, DomainRule rule, CancellationToken cancellationToken)
    {
        List<Guid> tagIds = rule.Actions.Select(static x => x.ClassificationId).OfType<Guid>().ToList();
        List<Guid> billIds = rule.Actions.Select(static x => x.BillId).OfType<Guid>().ToList();
        List<Guid> debtIds = rule.Actions.Select(static x => x.DebtId).OfType<Guid>().ToList();
        List<Guid> accountIds = AccountsOf(rule).ToList();

        Dictionary<Guid, string> names = await context.CustomClassifications.AsNoTracking()
            .Where(x => tagIds.Contains(x.Id))
            .ToDictionaryAsync(static x => x.Id, static x => x.Tag, cancellationToken);
        foreach (var bill in await context.Bills.AsNoTracking().Where(x => billIds.Contains(x.Id))
                     .Select(static x => new { x.Id, x.Name }).ToListAsync(cancellationToken))
        {
            names[bill.Id] = bill.Name;
        }

        foreach (var debt in await context.Debts.AsNoTracking().Where(x => debtIds.Contains(x.Id))
                     .Select(static x => new { x.Id, x.Name }).ToListAsync(cancellationToken))
        {
            names[debt.Id] = debt.Name;
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
