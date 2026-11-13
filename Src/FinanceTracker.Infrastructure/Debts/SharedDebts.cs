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
    public static async Task<List<VisibleItem<DomainDebt>>> ForAsync(FinanceTrackerContext context, Guid userId,
        AccountsInView view, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(view);
        List<VisibleItem<DomainDebt>> chosen = await SharedChosenItems.ForAsync(context, context.Debts, userId,
            ShareItemType.Debt, view.IsJustMine, cancellationToken);

        // Following an account they see that's someone else's, shared as the account is
        IReadOnlyList<Guid> sharedAccounts = view.SharedAccountIds;
        List<DomainDebt> followingAccounts = await context.Debts.AsNoTracking()
            .Where(x => x.UserId != userId && x.AccountId != null && sharedAccounts.Contains(x.AccountId.Value))
            .ToListAsync(cancellationToken);

        return await SharedChosenItems.WithFollowingAsync(context, chosen, followingAccounts,
            static x => x.AccountId!.Value, view, cancellationToken);
    }

    /// <summary>
    /// Each of <paramref name="debts"/>' figures, by debt: the account it follows, and its payments from the accounts
    /// its owner sees. The same as its owner gets, whoever's asking.
    /// </summary>
    /// <param name="all">Every account the user sees, which their own debts' payments come from.</param>
    public static async Task<Dictionary<Guid, DebtFigures>> FiguresAsync(FinanceTrackerContext context,
        AccountsInView all, IReadOnlyCollection<VisibleItem<DomainDebt>> debts, CancellationToken cancellationToken)
    {
        List<Guid> accountIds = debts.Select(static x => x.Item.AccountId).OfType<Guid>().Distinct().ToList();
        Dictionary<Guid, BankAccount> accounts = await context.BankAccounts.AsNoTracking()
            .Where(x => accountIds.Contains(x.Id))
            .ToDictionaryAsync(static x => x.Id, cancellationToken);

        Dictionary<Guid, DebtFigures> figures = [];
        foreach (IGrouping<Guid, VisibleItem<DomainDebt>> owner in debts.GroupBy(static x => x.Item.UserId))
        {
            AccountsInView ownersView = all;
            if (owner.Key != all.UserId)
            {
                ownersView = await AccountScope.ResolveAllForAsync(context, owner.Key, cancellationToken);
            }

            List<Guid> debtIds = owner.Select(static x => x.Item.Id).ToList();
            ILookup<Guid, BankTransaction> payments = (await context.BankTransactions.InView(ownersView)
                    .AsNoTracking()
                    .Where(x => x.DebtPayment != null && x.DebtPayment.DebtId != null &&
                                debtIds.Contains(x.DebtPayment.DebtId.Value))
                    .ToListAsync(cancellationToken))
                .ToLookup(static x => x.DebtPayment!.DebtId!.Value);

            foreach (VisibleItem<DomainDebt> debt in owner)
            {
                BankAccount? account = debt.Item.AccountId is { } accountId
                    ? accounts.GetValueOrDefault(accountId)
                    : null;
                figures[debt.Item.Id] = new DebtFigures(account, payments[debt.Item.Id].ToList());
            }
        }

        return figures;
    }
}
