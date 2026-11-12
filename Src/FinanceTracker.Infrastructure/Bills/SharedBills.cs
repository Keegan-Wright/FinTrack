using FinanceTracker.Domain.Sharing;
using FinanceTracker.Infrastructure.Sharing;
using Microsoft.EntityFrameworkCore;
using DomainBill = FinanceTracker.Domain.Bills.Bill;

namespace FinanceTracker.Infrastructure.Bills;

/// <summary>
/// The bills a user sees. Their own, those paid from an account shared with them, shared the same way as it, and those
/// another person chose to share with them, see <see cref="ShareItemType.Bill"/>. Their own on the hidden copy of a
/// merged joint account are left out where the shown copy has the same bill, so it shows once.
/// </summary>
internal static class SharedBills
{
    /// <summary>
    /// Every bill <paramref name="userId"/> sees through <paramref name="view"/>, untracked, their own first.
    /// </summary>
    /// <param name="view">Their accounts and those shared with them. A bill paid from one shared with changes allowed
    /// can be changed. Just mine leaves out those chosen for them, as it does accounts.</param>
    public static async Task<List<VisibleItem<DomainBill>>> ForAsync(FinanceTrackerContext context, Guid userId,
        AccountsInView view, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(view);
        List<VisibleItem<DomainBill>> chosen = await SharedChosenItems.ForAsync(context, context.Bills, userId,
            ShareItemType.Bill, view.IsJustMine, cancellationToken);

        // Paid from an account they see that's someone else's, shared as the account is
        IReadOnlyList<Guid> sharedAccounts = view.SharedAccountIds;
        List<DomainBill> followingAccounts = await context.Bills.AsNoTracking()
            .Where(x => x.UserId != userId && sharedAccounts.Contains(x.AccountId))
            .ToListAsync(cancellationToken);

        List<VisibleItem<DomainBill>> bills = await SharedChosenItems.WithFollowingAsync(context, chosen,
            followingAccounts, static x => x.AccountId, view, cancellationToken);
        return bills.Where(x => !x.IsOwn || !StandsInForIt(x.Item, view, followingAccounts)).ToList();
    }

    /// <summary>
    /// Whether <paramref name="own"/> is on the user's hidden copy of a merged joint account, and the shown copy has the
    /// same bill, which stands for both. Each copy's bills come from its own connection, so the same standing order or
    /// direct debit is a bill on each.
    /// </summary>
    /// <param name="othersBills">Other people's bills on accounts shared with the user, the shown copy's among them.
    /// </param>
    private static bool StandsInForIt(DomainBill own, AccountsInView view, List<DomainBill> othersBills)
    {
        if (!view.HiddenAccountIds.Contains(own.AccountId) ||
            !view.FillingAccounts.TryGetValue(own.AccountId, out Guid shownId))
        {
            return false;
        }

        return othersBills.Exists(x => x.AccountId == shownId && IsSameBill(x, own));
    }

    /// <summary>
    /// Whether two copies' bills are one bill: from the same kind of source, to the same payee, as often.
    /// </summary>
    private static bool IsSameBill(DomainBill first, DomainBill second) =>
        first.Source == second.Source &&
        first.Frequency == second.Frequency &&
        string.Equals(first.Payee.Trim(), second.Payee.Trim(), StringComparison.OrdinalIgnoreCase);
}
