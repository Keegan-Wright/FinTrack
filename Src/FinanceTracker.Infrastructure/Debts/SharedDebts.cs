using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.Sharing;
using FinanceTracker.Infrastructure.Sharing;
using Microsoft.EntityFrameworkCore;
using DomainDebt = FinanceTracker.Domain.Debts.Debt;

namespace FinanceTracker.Infrastructure.Debts;

/// <summary>
/// The debts a user sees. Their own, those following an account shared with them, a loan or card, shared the same way
/// as it, and those another person chose to share with them, see <see cref="ShareItemType.Debt"/>.
/// </summary>
internal static class SharedDebts
{
    /// <summary>
    /// Every debt <paramref name="userId"/> sees through <paramref name="view"/>, untracked, their own first.
    /// </summary>
    /// <param name="view">Their accounts and those shared with them. A debt following one shared with changes allowed
    /// can be changed. Just mine leaves out those chosen for them, as it does accounts.</param>
    public static async Task<List<VisibleDebt>> ForAsync(FinanceTrackerContext context, Guid userId,
        AccountsInView view, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(view);

        List<DomainDebt> own = await context.Debts.OwnedBy(userId).AsNoTracking().ToListAsync(cancellationToken);

        // Following an account they see that's someone else's, shared as the account is
        IReadOnlyList<Guid> sharedAccounts = view.SharedAccountIds;

        List<DomainDebt> followingAccounts = await context.Debts.AsNoTracking()
            .Where(x => x.UserId != userId && x.AccountId != null && sharedAccounts.Contains(x.AccountId.Value))
            .ToListAsync(cancellationToken);

        IQueryable<Share> chosenShares = context.ChosenSharesWith(userId, ShareItemType.Debt);
        if (view.IsJustMine)
        {
            chosenShares = chosenShares.Where(static _ => false);
        }

        var chosen = await (
                from share in chosenShares
                join debt in context.Debts on share.ItemId equals debt.Id
                where debt.UserId == share.UserId
                select new { Debt = debt, share.Mode })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        Dictionary<Guid, (DomainDebt Debt, bool CanChange)> others = [];
        foreach (DomainDebt debt in followingAccounts)
        {
            others[debt.Id] = (debt, view.ChangeableAccountIds.Contains(debt.AccountId!.Value));
        }

        foreach (var shared in chosen)
        {
            bool canChange = shared.Mode == ShareMode.AllowChanges;
            if (others.TryGetValue(shared.Debt.Id, out (DomainDebt Debt, bool CanChange) already))
            {
                canChange |= already.CanChange;
            }

            others[shared.Debt.Id] = (shared.Debt, canChange);
        }

        List<Guid> ownerIds = others.Values.Select(static x => x.Debt.UserId).Distinct().ToList();

        Dictionary<Guid, string> names = await context.Users.AsNoTracking()
            .Where(x => ownerIds.Contains(x.Id))
            .ToDictionaryAsync(static x => x.Id, static x => $"{x.FirstName} {x.LastName}".Trim(), cancellationToken);

        return
        [
            .. own.Select(static x => new VisibleDebt(x, null, true)),
            .. others.Values.Select(x =>
                new VisibleDebt(x.Debt, names.GetValueOrDefault(x.Debt.UserId, string.Empty), x.CanChange))
        ];
    }

    /// <summary>
    /// Each of <paramref name="debts"/>' figures, by debt: the account it follows, and its payments from the accounts
    /// its owner sees. The same as its owner gets, whoever's asking.
    /// </summary>
    /// <param name="all">Every account the user sees, which their own debts' payments come from.</param>
    public static async Task<Dictionary<Guid, DebtFigures>> FiguresAsync(FinanceTrackerContext context,
        AccountsInView all, IReadOnlyCollection<VisibleDebt> debts, CancellationToken cancellationToken)
    {
        List<Guid> accountIds = debts.Select(static x => x.Debt.AccountId).OfType<Guid>().Distinct().ToList();
        Dictionary<Guid, BankAccount> accounts = await context.BankAccounts.AsNoTracking()
            .Where(x => accountIds.Contains(x.Id))
            .ToDictionaryAsync(static x => x.Id, cancellationToken);

        Dictionary<Guid, DebtFigures> figures = [];
        foreach (IGrouping<Guid, VisibleDebt> owner in debts.GroupBy(static x => x.Debt.UserId))
        {
            AccountsInView ownersView = all;
            if (owner.Key != all.UserId)
            {
                ownersView = await AccountScope.ResolveAllForAsync(context, owner.Key, cancellationToken);
            }

            List<Guid> debtIds = owner.Select(static x => x.Debt.Id).ToList();
            ILookup<Guid, BankTransaction> payments = (await context.BankTransactions.InView(ownersView)
                    .AsNoTracking()
                    .Where(x => x.DebtPayment != null && x.DebtPayment.DebtId != null &&
                                debtIds.Contains(x.DebtPayment.DebtId.Value))
                    .ToListAsync(cancellationToken))
                .ToLookup(static x => x.DebtPayment!.DebtId!.Value);

            foreach (VisibleDebt debt in owner)
            {
                BankAccount? account = debt.Debt.AccountId is { } accountId
                    ? accounts.GetValueOrDefault(accountId)
                    : null;
                figures[debt.Debt.Id] = new DebtFigures(account, payments[debt.Debt.Id].ToList());
            }
        }

        return figures;
    }
}
